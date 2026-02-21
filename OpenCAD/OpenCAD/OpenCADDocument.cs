using OpenCAD.Containers;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Settings;
using OpenCAD.Styles.LineTypes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using OpenCAD.Undo;

namespace OpenCAD
{
    /// <summary>
    /// Represents a root CAD document/file with associated metadata.
    /// Child objects represent geometry, settings, and other drawing elements.
    /// </summary>
    public class OpenCADDocument : OpenCADObject
    {
        public enum UnitFormatType
        {
            Linear,
            Angular,
            Vector3D,
        }

        public const uint ContinuousLineTypeID = 0; // Reserved ID for the default continuous line type
        public const uint LineTypeByLayer = uint.MaxValue;

        // Add volatile to ensure visibility across threads
        private volatile IServiceProvider? _serviceProvider;

        public OpenCADDocument()
        {
            // Initialize string properties with names (filename and description)
            Filename = string.Empty;
            Description = string.Empty;
            _document = this;

            // Create the specialized layers container
            var layersContainer = new OpenCADLayers(this);
            Add(layersContainer);

            LayersContainerID = layersContainer.ID;

            // Create default "0" layer (standard in CAD systems)
            var defaultLayer = new OpenCADLayer(OpenCADStrings.DefaultLayerName, Color.White, ContinuousLineTypeID, LineWeight.Default, this);
            layersContainer.AddLayer(defaultLayer);
            CurrentLayer = defaultLayer;
            CurrentLineTypeID = LineTypeByLayer;
            CurrentLineWeight = LineWeight.ByLayer;
            CurrentColor = Color.FromArgb(0,0,0,0); // ByLayer

            // Create the specialized text styles container
            var textStylesContainer = new OpenCADTextStyles(this);
            Add(textStylesContainer);
            TextStylesContainerID = textStylesContainer.ID;

            var defaultTextStyle = new OpenCADTextStyle(OpenCADStrings.DefaultTextStyleName, "Arial", 1.0, this);
            textStylesContainer.AddTextStyle(defaultTextStyle);
            CurrentTextStyle = defaultTextStyle;

            // Add other default settings objects as children if needed
            var viewportSettings = new ViewportSettings(this);
            Add(viewportSettings);

            CurrentViewportSettingsID = viewportSettings.ID;
            LastGeometricChild = Guid.Empty;

            // Add the line types container
            var lineTypesContainer = new OpenCADLineTypes(this);
            Add(lineTypesContainer);
            LineTypesContainerID = lineTypesContainer.ID;

            var undoRedoManager = new UndoRedoManager(this);
            Add(undoRedoManager);
            UndoRedoManagerID = undoRedoManager.ID;
        }

        public OpenCADDocument(string filename, string description = "") : this()
        {
            Filename = filename;
            Description = description;
            _document = this;
        }

        /// <summary>
        /// Ensures the document is fully initialized after deserialization.
        /// Call this after loading from JSON to verify all properties are accessible.
        /// </summary>
        public bool EnsureInitialized()
        {
            try
            {
                // Force properties to be accessed to trigger any lazy initialization
                var test1 = Filename;
                var test2 = Description;
                var test3 = CurrentLayer;
                var test4 = GetLayers().ToList();
                
                // If we got here without exception, document is initialized
                //System.Diagnostics.Debug.WriteLine($"Document initialized: {test4.Count} layers, current: {test3?.Name ?? "null"}");
                return true;
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"Document NOT initialized: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called after deserialization to rebuild caches and restore references.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            //System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized START ===");
            
            try
            {
                // Recursively restore document and parent references for all children
                //System.Diagnostics.Debug.WriteLine("Restoring references...");
                RestoreReferences(this, this);
                
                // Rebuild the layer cache in the layers container
                var layersContainer = GetLayersContainer();
                if (layersContainer != null)
                {
                    //System.Diagnostics.Debug.WriteLine($"Rebuilding layer cache...");
                    layersContainer.RebuildCache();
                    
                    var layers = layersContainer.GetLayers().ToList();
                    //System.Diagnostics.Debug.WriteLine($"Found {layers.Count} layers");
                    
                    foreach (var layer in layers)
                    {
                        //System.Diagnostics.Debug.WriteLine($"  Layer: {layer.Name} (ID: {layer.ID})");
                        // Restore document reference
                        layer.Document = this;
                    }
                }
                
                // Rebuild the text style cache in the text styles container
                var textStylesContainer = GetTextStylesContainer();
                if (textStylesContainer != null)
                {
                    //System.Diagnostics.Debug.WriteLine($"Rebuilding text style cache...");
                    textStylesContainer.RebuildCache();
                    
                    var textStyles = textStylesContainer.GetTextStyles().ToList();
                    //System.Diagnostics.Debug.WriteLine($"Found {textStyles.Count} text styles");
                    
                    foreach (var textStyle in textStyles)
                    {
                        //System.Diagnostics.Debug.WriteLine($"  Text Style: {textStyle.Name} (ID: {textStyle.ID})");
                        // Restore document reference
                        textStyle.Document = this;
                    }
                }
                
                //System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized COMPLETE ===");
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"=== OpenCADDocument.OnDeserialized FAILED: {ex.Message} ===");
                //System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Recursively restores document and parent references after deserialization.
        /// </summary>
        private void RestoreReferences(OpenCADObject parent, OpenCADDocument document)
        {
            foreach (var child in parent.GetChildren())
            {
                child.Document = document;
                child.Parent = parent;
                
                // Recursively process grandchildren
                RestoreReferences(child, document);
            }
        }

        /// <summary>
        /// Gets or sets the filename of the CAD document.
        /// </summary>
        [JsonIgnore]
        public string Filename
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Filename));
            set => SetPropertyValue(PropertyType.String, nameof(Filename), OpenCADStrings.Filename, value);
        }

        /// <summary>
        /// Gets or sets the description of the CAD document.
        /// </summary>
        [JsonIgnore]
        public string Description
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Description)) ?? string.Empty;
            set => SetPropertyValue(PropertyType.String, nameof(Description), OpenCADStrings.Description, value);
        }

        /// <summary>
        /// Gets or sets the current active layer for new objects.
        /// </summary>
        [JsonIgnore]
        public OpenCADLayer CurrentLayer
        {
            get
            {
                var layersContainer = GetLayersContainer();
                return layersContainer?.GetLayer(CurrentLayerID) ?? new OpenCADLayer();
            }
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentLayerID), OpenCADStrings.CurrentLayerID, value.ID);
        }

        /// <summary>
        /// Gets or sets the current active layer's Id for new objects.
        /// </summary>
        [JsonIgnore]
        public Guid CurrentLayerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(CurrentLayerID));
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentLayerID), OpenCADStrings.CurrentLayerID, value);
        }

        /// <summary>
        /// Gets or sets the current active text style for new objects.
        /// </summary>
        [JsonIgnore]
        public OpenCADTextStyle CurrentTextStyle
        {
            get
            {
                var textStylesContainer = GetTextStylesContainer();
                return textStylesContainer?.GetTextStyle(CurrentTextStyleID) ?? new OpenCADTextStyle();
            }
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentTextStyleID), OpenCADStrings.CurrentTextStyleID, value.ID);
        }

        /// <summary>
        /// Gets or sets the current active text style's Id for new objects.
        /// </summary>
        [JsonIgnore]
        public Guid CurrentTextStyleID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(CurrentTextStyleID));
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentTextStyleID), OpenCADStrings.CurrentTextStyleID, value);
        }

        [JsonIgnore]
        public Guid? CurrentViewportSettingsID
        {
            get => GetPropertyValue<Guid?>(PropertyType.ID, nameof(CurrentViewportSettingsID));
            private set => SetPropertyValue(PropertyType.ID, nameof(CurrentViewportSettingsID), OpenCADStrings.CurrentViewportSettingsID, value);
        }

        /// <summary>
        /// Gets or sets the current color for new objects.
        /// If null, new objects will use ByLayer color.
        /// </summary>
        [JsonIgnore]
        public Color CurrentColor
        {
            get => GetPropertyValue<Color>(PropertyType.Color, nameof(CurrentColor));
            set => SetPropertyValue(PropertyType.Color, nameof(CurrentColor), OpenCADStrings.CurrentColor, value);
        }

        /// <summary>
        /// Gets or sets the current line type for new objects.
        /// If null, new objects will use ByLayer line type.
        /// </summary>
        [JsonIgnore]
        public uint? CurrentLineTypeID
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(CurrentLineTypeID));
            set => SetPropertyValue(PropertyType.UInt, nameof(CurrentLineTypeID), OpenCADStrings.CurrentLineType, value);
        }

        /// <summary>
        /// Gets or sets the current line weight for new objects.
        /// If null, new objects will use ByLayer line weight.
        /// </summary>
        [JsonIgnore]
        public LineWeight? CurrentLineWeight
        {
            get => GetPropertyValue<LineWeight>(PropertyType.LineWeight, nameof(CurrentLineWeight));
            set => SetPropertyValue(PropertyType.LineWeight, nameof(CurrentLineWeight), OpenCADStrings.CurrentLineWeight, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid? LastGeometricChild
        {
            get => GetPropertyValue<Guid?>(PropertyType.ID, nameof(LastGeometricChild));
            private set => SetPropertyValue(PropertyType.ID, nameof(LastGeometricChild), OpenCADStrings.LastGeometricChild, value);
        }

        /// <summary>
        /// Gets or sets the last text height used in the document.
        /// This serves as the default for the next TEXT command.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double LastTextHeight
        {
            get
            {
                var value = GetPropertyValue<double>(PropertyType.DoubleLength, nameof(LastTextHeight));
                // Return the value if it exists and is valid, otherwise return default text style height
                return value > 0 ? value : CurrentTextStyle?.FontSize ?? 1.0;
            }
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(LastTextHeight), OpenCADStrings.LastTextHeight, value);
        }

        [JsonIgnore, XmlIgnore]
        public double LastDistance
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(LastDistance));
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(LastDistance), OpenCADStrings.LastDistance, value);
        }

        public override bool Add(OpenCADObject obj)
        {
            var added = base.Add(obj);
            if (added && obj is ICurve)
            {
                LastGeometricChild = obj.ID;

                // Notify listeners that an object was added
                ObjectAdded?.Invoke(this, new DocumentObjectEventArgs(obj));
            }
            return added;
        }

        /// <summary>
        /// Remove an object from the document and notify listeners.
        /// </summary>
        public override bool Remove(OpenCADObject obj)
        {
            if (obj == null) return false;

            var removed = base.Remove(obj);
            if (removed)
            {
                // If the removed object was the last geometric child, clear the record.
                if (obj is IDrawable && LastGeometricChild.HasValue && LastGeometricChild.Value == obj.ID)
                {
                    LastGeometricChild = Guid.Empty;
                }

                ObjectRemoved?.Invoke(this, new DocumentObjectEventArgs(obj));
            }
            return removed;
        }

        public void ReplaceObject(OpenCADObject oldObj, OpenCADObject newObj)
        {
            Remove(oldObj);
            Add(newObj);
            NotifyObjectChanged(newObj);
        }

        /// <summary>
        /// Notify listeners that an object changed in-place.
        /// Call this after mutating an existing object's properties.
        /// </summary>
        public void NotifyObjectChanged(OpenCADObject obj)
        {
            if (obj == null) return;
            ObjectChanged?.Invoke(this, new DocumentObjectEventArgs(obj));
        }

        /// <summary>
        /// Events raised when the document's object graph is modified.
        /// Consumers (viewmodels) should subscribe to keep UI in sync.
        /// </summary>
        public event EventHandler<DocumentObjectEventArgs>? ObjectAdded;

        public event EventHandler<DocumentObjectEventArgs>? ObjectRemoved;

        public event EventHandler<DocumentObjectEventArgs>? ObjectChanged;

        public ICurve? GetLastGeometricChild()
        {
            if (LastGeometricChild.HasValue && LastGeometricChild != Guid.Empty)
            {
                var obj = GetChild(LastGeometricChild.Value);
                return obj as ICurve;
            }
            return null;
        }

        #region Layer Management - Delegates to OpenCADLayers Container

        /// <summary>
        /// Adds a new layer to the document.
        /// </summary>
        /// <param name="layer">The layer to add.</param>
        /// <returns>True if the layer was added successfully, false if a layer with the same name already exists.</returns>
        public bool AddLayer(OpenCADLayer layer)
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.AddLayer(layer) ?? false;
        }

        /// <summary>
        /// Creates and adds a new layer to the document.
        /// </summary>
        /// <param name="name">The name of the new layer.</param>
        /// <param name="color">The default color for the layer.</param>
        /// <param name="lineType">The default line type for the layer.</param>
        /// <param name="lineWeight">The default line weight for the layer.</param>
        /// <returns>The newly created layer, or null if a layer with the same name already exists.</returns>
        public OpenCADLayer? CreateLayer(string name, Color? color = null, uint? lineTypeID = null, LineWeight? lineWeight = null)
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.CreateLayer(name, color, lineTypeID, lineWeight);
        }

        /// <summary>
        /// Gets a layer by name.
        /// </summary>
        /// <param name="name">The name of the layer to retrieve.</param>
        /// <returns>The layer with the specified name, or null if not found.</returns>
        public OpenCADLayer? GetLayer(string name)
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.GetLayer(name);
        }

        /// <summary>
        /// Gets a layer by ID.
        /// </summary>
        /// <param name="layerId">The ID of the layer to retrieve.</param>
        /// <returns>The layer with the specified ID, or null if not found.</returns>
        public OpenCADLayer? GetLayer(Guid layerId)
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.GetLayer(layerId);
        }

        /// <summary>
        /// Removes a layer from the document.
        /// Layer "0" cannot be removed.
        /// </summary>
        /// <param name="name">The name of the layer to remove.</param>
        /// <returns>True if the layer was removed successfully, false otherwise.</returns>
        public bool RemoveLayer(string name)
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.RemoveLayer(name) ?? false;
        }

        /// <summary>
        /// Gets all layers in the document.
        /// </summary>
        public IEnumerable<OpenCADLayer> GetLayers()
        {
            var layersContainer = GetLayersContainer();
            return layersContainer?.GetLayers() ?? Enumerable.Empty<OpenCADLayer>();
        }

        /// <summary>
        /// Gets all line types in the document.
        /// </summary>
        public IEnumerable<OpenCADLineType> GetLineTypes()
        {
            var lineTypesContainer = GetLineTypesContainer();
            return lineTypesContainer?.GetLineTypes() ?? Enumerable.Empty<OpenCADLineType>();
        }


        /// <summary>
        /// Sets the current layer by name.
        /// </summary>
        /// <param name="name">The name of the layer to set as current.</param>
        /// <returns>True if the layer was found and set as current, false otherwise.</returns>
        public bool SetCurrentLayer(string name)
        {
            var layer = GetLayer(name);
            if (layer != null)
            {
                CurrentLayer = layer;
                return true;
            }
            return false;
        }

        #endregion

        #region Text Style Management - Delegates to OpenCADTextStyles Container

        /// <summary>
        /// Adds a new text style to the document.
        /// </summary>
        /// <param name="textStyle">The text style to add.</param>
        /// <returns>True if the text style was added successfully, false if a text style with the same name already exists.</returns>
        public bool AddTextStyle(OpenCADTextStyle textStyle)
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.AddTextStyle(textStyle) ?? false;
        }

        /// <summary>
        /// Creates and adds a new text style to the document.
        /// </summary>
        /// <param name="name">The name of the new text style.</param>
        /// <param name="fontFamily">The font family for the text style.</param>
        /// <param name="fontSize">The font size for the text style.</param>
        /// <returns>The newly created text style, or null if a text style with the same name already exists.</returns>
        public OpenCADTextStyle? CreateTextStyle(string name, string fontFamily, double fontSize)
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.CreateTextStyle(name, fontFamily, fontSize);
        }

        /// <summary>
        /// Gets a text style by name.
        /// </summary>
        /// <param name="name">The name of the text style to retrieve.</param>
        /// <returns>The text style with the specified name, or null if not found.</returns>
        public OpenCADTextStyle? GetTextStyle(string name)
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.GetTextStyle(name);
        }

        /// <summary>
        /// Gets a text style by ID.
        /// </summary>
        /// <param name="textStyleId">The ID of the text style to retrieve.</param>
        /// <returns>The text style with the specified ID, or null if not found.</returns>
        public OpenCADTextStyle? GetTextStyle(Guid textStyleId)
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.GetTextStyle(textStyleId);
        }

        /// <summary>
        /// Removes a text style from the document.
        /// The default text style cannot be removed.
        /// </summary>
        /// <param name="name">The name of the text style to remove.</param>
        /// <returns>True if the text style was removed successfully, false otherwise.</returns>
        public bool RemoveTextStyle(string name)
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.RemoveTextStyle(name) ?? false;
        }

        /// <summary>
        /// Gets all text styles in the document.
        /// </summary>
        public IEnumerable<OpenCADTextStyle> GetTextStyles()
        {
            var textStylesContainer = GetTextStylesContainer();
            return textStylesContainer?.GetTextStyles() ?? Enumerable.Empty<OpenCADTextStyle>();
        }

        /// <summary>
        /// Sets the current text style by name.
        /// </summary>
        /// <param name="name">The name of the text style to set as current.</param>
        /// <returns>True if the text style was found and set as current, false otherwise.</returns>
        public bool SetCurrentTextStyle(string name)
        {
            var textStyle = GetTextStyle(name);
            if (textStyle != null)
            {
                CurrentTextStyle = textStyle;
                return true;
            }
            return false;
        }

        #endregion

        /// <summary>
        /// Applies the document's current properties to a new object.
        /// </summary>
        /// <param name="obj">The object to apply properties to.</param>
        public void ApplyCurrentProperties(IDrawable obj)
        {
            if (obj == null)
                return;

            obj.Layer = CurrentLayer;
            obj.Color = CurrentColor;
            obj.LineTypeID = CurrentLineTypeID ?? ContinuousLineTypeID;
            obj.LineWeight = CurrentLineWeight ?? LineWeight.ByLayer;
        }

        [JsonIgnore, XmlIgnore]
        public bool HasUnsavedChanges { get; set; }

        [JsonIgnore, XmlIgnore]
        public Guid LayersContainerID 
        { 
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(LayersContainerID));
            private set => SetPropertyValue(PropertyType.ID, nameof(LayersContainerID), OpenCADStrings.LayersContainerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid LineTypesContainerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(LineTypesContainerID));
            private set => SetPropertyValue(PropertyType.ID, nameof(LineTypesContainerID), OpenCADStrings.LineTypesContainerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid TextStylesContainerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(TextStylesContainerID));
            private set => SetPropertyValue(PropertyType.ID, nameof(TextStylesContainerID), OpenCADStrings.TextStylesContainerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid UndoRedoManagerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(UndoRedoManagerID));
            set => SetPropertyValue(PropertyType.ID, nameof(UndoRedoManagerID), OpenCADStrings.UndoRedoManagerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public IServiceProvider? ServiceProvider
        {
            get => _serviceProvider;
            set => _serviceProvider = value;
        }

        [JsonIgnore, XmlIgnore]
        public Point3D? PreviewPoint { get; set; }

        /// <summary>
        /// Thread-safe service resolution.
        /// </summary>
        public T? GetService<T>() where T : class
        {
            var provider = _serviceProvider; // Read once
            if (provider == null)
                return null;

            try
            {
                return provider.GetService(typeof(T)) as T;
            }
            catch (ObjectDisposedException)
            {
                // Service provider might be disposed during shutdown
                return null;
            }
        }

        // Call this whenever the document is modified
        public void MarkAsModified()
        {
            HasUnsavedChanges = true;
        }
        
        // Call this after saving
        public void MarkAsSaved()
        {
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Initializes the document after deserialization.
        /// MUST be called after loading from JSON!
        /// </summary>
        public void InitializeAfterDeserialization()
        {
            _document = this;

            // Restore parent/document recursively
            RestoreReferences(this, this);

            // Rebuild layer cache in the layers container
            var layersContainer = GetLayersContainer();
            if (layersContainer != null)
            {
                layersContainer.RebuildCache();
                foreach (var layer in layersContainer.GetLayers())
                {
                    layer.Document = this;
                }
            }

            // Rebuild text style cache in the text styles container
            var textStylesContainer = GetTextStylesContainer();
            if (textStylesContainer != null)
            {
                textStylesContainer.RebuildCache();
                foreach (var textStyle in textStylesContainer.GetTextStyles())
                {
                    textStyle.Document = this;
                }
            }
        }

        public UndoRedoManager? GetUndoRedoManager()
        {
            return GetChild(UndoRedoManagerID) as UndoRedoManager;
        }

        /// <summary>
        /// Gets the layers container.
        /// </summary>
        public OpenCADLayers? GetLayersContainer()
        {
            return GetChild(LayersContainerID) as OpenCADLayers;
        }

        /// <summary>
        /// Gets the line types container.
        /// </summary>
        public OpenCADLineTypes? GetLineTypesContainer()
        {
            return GetChild(LineTypesContainerID) as OpenCADLineTypes;
        }

        /// <summary>
        /// Gets the text styles container.
        /// </summary>
        private OpenCADTextStyles? GetTextStylesContainer()
        {
            return GetChild(TextStylesContainerID) as OpenCADTextStyles;
        }

        public ViewportSettings? GetViewportSettings()
        {
            if (CurrentViewportSettingsID.HasValue)
            {
                return GetChild(CurrentViewportSettingsID.Value) as ViewportSettings;
            }
            return null;
        }

        public string ValueToString(double value, UnitFormatType type)
        {
            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return type switch
                {
                    UnitFormatType.Linear => unitSettings.LengthToString(value),
                    UnitFormatType.Angular => unitSettings.AngleToString(value),
                    _ => value.ToString()
                };
            }
            return value.ToString();
        }

        public double StringToValue(string str, UnitFormatType type)
        {
            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return type switch
                {
                    UnitFormatType.Linear => unitSettings.StringToLength(str),
                    UnitFormatType.Angular => unitSettings.StringToAngle(str),
                    _ => double.Parse(str)
                };
            }
            return double.Parse(str);
        }

        public string VectorToString(Vector3D vector)
        {
            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                var xStr = unitSettings.LengthToString(vector.X);
                var yStr = unitSettings.LengthToString(vector.Y);
                var zStr = unitSettings.LengthToString(vector.Z);
                return $"X:{xStr}, Y:{yStr}, Z:{zStr}";
            }

            return vector.ToString();
        }

        public Vector3D StringToVector(string str)
        {
            // Example input: "X:1.23, Y:4.56, Z:7.89"
            (double x, double y, double z) = ParsePointComponents(str);

            return new Vector3D(x, y, z);
        }

        public string PointToString(Point3D point)
        {

            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return VectorToString(point.AsVector3D());
            }

            return point.ToString();
        }

        public Point3D StringToPoint(string str)
        {
            // Example input: "X:1.23, Y:4.56, Z:7.89"
            (double x, double y, double z) = ParsePointComponents(str);

            return new Point3D(x, y, z);
        }

        private (double x, double y, double z) ParsePointComponents(string strValue)
        {
            var components = strValue.Split(',');
            if (components.Length != 3)
                throw new FormatException("Invalid point format. Expected format: \"X:{x}, Y:{y}, Z:{z}\"");
            try
            {
                double x = double.Parse(components[0].Substring(2).Trim());
                double y = double.Parse(components[1].Substring(2).Trim());
                double z = double.Parse(components[2].Substring(2).Trim());
                return (x, y, z);
            }
            catch (Exception ex)
            {
                throw new FormatException("Invalid point format.", ex);
            }
        }

        public string ColorToString(Color value)
        {
            return $"A{value.A} R{value.R} G{value.G} B{value.B}";
        }

        public Color StringToColor(string strValue)
        {
            if (string.IsNullOrWhiteSpace(strValue))
                throw new ArgumentNullException(nameof(strValue));

            if (strValue.Equals(OpenCADStrings.ByLayer, StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(0, 0, 0, 0); // ByLayer

            var components = strValue.Split(' ');
            if (components.Length != 4)
                throw new FormatException("Invalid color format. Expected format: \"A{alpha} R{red} G{green} B{blue}\"");

            try
            {
                byte a = byte.Parse(components[0].Substring(1));
                byte r = byte.Parse(components[1].Substring(1));
                byte g = byte.Parse(components[2].Substring(1));
                byte b = byte.Parse(components[3].Substring(1));
                return Color.FromArgb(a, r, g, b);
            }
            catch (Exception ex)
            {
                throw new FormatException("Invalid color format.", ex);
            }
        }
    }

    /// <summary>
    /// Event args for document object changes.
    /// </summary>
    public class DocumentObjectEventArgs : EventArgs
    {
        public OpenCADObject Object { get; }

        public DocumentObjectEventArgs(OpenCADObject obj)
        {
            Object = obj ?? throw new ArgumentNullException(nameof(obj));
        }
    }
}