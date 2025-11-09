namespace OpenCAD
{
    /// <summary>
    /// Central repository for all string literals used in the OpenCAD project.
    /// This ensures consistency and makes localization easier in the future.
    /// </summary>
    public static class OpenCADStrings
    {
        #region Property Names

        /// <summary>
        /// Property name for color properties
        /// </summary>
        public const string Color = "Color";

        /// <summary>
        /// Property name for line type properties
        /// </summary>
        public const string LineType = "LineType";

        /// <summary>
        /// Property name for line weight properties
        /// </summary>
        public const string LineWeight = "LineWeight";

        /// <summary>
        /// Property name for filename property
        /// </summary>
        public const string Filename = "Filename";

        /// <summary>
        /// Property name for description property
        /// </summary>
        public const string Description = "Description";

        /// <summary>
        /// Property name for current layer ID
        /// </summary>
        public const string CurrentLayerID = "Current Layer ID";

        /// <summary>
        /// Property name for current color
        /// </summary>
        public const string CurrentColor = "Current Color";

        /// <summary>
        /// Property name for current line type
        /// </summary>
        public const string CurrentLineType = "Current LineType";

        /// <summary>
        /// Property name for current line weight
        /// </summary>
        public const string CurrentLineWeight = "Current LineWeight";

        /// <summary>
        /// Property name for layers container ID
        /// </summary>
        public const string LayersContainer = "Layers Container";

        /// <summary>
        /// Property name for layers container ID
        /// </summary>
        public const string LayersContainerID = "Layers Container ID";

        /// <summary>
        /// Property name for viewport settings container ID
        /// </summary>
        public const string ViewportSettingsContainerID = "Viewport Settings Container ID";

        /// <summary>
        /// Property name for layer name
        /// </summary>
        public const string LayerName = "Layer Name";

        /// <summary>
        /// Property name for layer color
        /// </summary>
        public const string LayerColor = "Layer Color";

        /// <summary>
        /// Property name for layer line type
        /// </summary>
        public const string LayerLineType = "Layer LineType";

        /// <summary>
        /// Property name for layer line weight
        /// </summary>
        public const string LayerLineWeight = "Layer LineWeight";

        /// <summary>
        /// Property name for layer visibility
        /// </summary>
        public const string LayerIsVisible = "Layer IsVisible";

        /// <summary>
        /// Property name for layer locked state
        /// </summary>
        public const string LayerIsLocked = "Layer IsLocked";

        /// <summary>
        /// Property name for start point
        /// </summary>
        public const string StartPoint = "Start Point";

        /// <summary>
        /// Property name for end point
        /// </summary>
        public const string EndPoint = "End Point";

        /// <summary>
        /// Property name for name
        /// </summary>
        public const string Name = "Name";

        /// <summary>
        /// Property name for status
        /// </summary>
        public const string Status = "Status";

        /// <summary>
        /// Property name for current layer column
        /// </summary>
        public const string Current = "Current";

        /// <summary>
        /// Property name for current layer column
        /// </summary>
        public const string On_Off = "On / Off";

        /// <summary>
        /// Property name for object type
        /// </summary>
        public const string ObjectType = "Object Type";

        /// <summary>
        /// Property name for object type
        /// </summary>
        public const string ObjectTypes = "Object Types";

        /// <summary>
        /// Property name for object ID
        /// </summary>
        public const string ObjectID = "Object ID";

        #endregion

        #region Default Layer Names

        /// <summary>
        /// Default layer name (standard in CAD systems)
        /// </summary>
        public const string DefaultLayerName = "0";

        /// <summary>
        /// Default name prefix for new layers
        /// </summary>
        public const string NewLayerPrefix = "Layer";

        #endregion

        #region File Extensions

        /// <summary>
        /// Default CAD file extension
        /// </summary>
        public const string CADFileExtension = ".cad";

        #endregion

        #region Display Strings

        /// <summary>
        /// Display string for null values
        /// </summary>
        public const string NullValue = "(null)";

        /// <summary>
        /// Display string for empty values
        /// </summary>
        public const string EmptyValue = "(empty)";

        /// <summary>
        /// Display string for "no document" state
        /// </summary>
        public const string NoDocument = "No document";

        /// <summary>
        /// Placeholder display value
        /// </summary>
        public const string PlaceholderValue = "—";

        /// <summary>
        /// Display string for "no layers" state
        /// </summary>
        public const string NoLayers = "No layers available";

        #endregion

        #region Document Property Names

        /// <summary>
        /// Display name for document type
        /// </summary>
        public const string DocumentType = "Type";

        /// <summary>
        /// Display name for document ID
        /// </summary>
        public const string DocumentID = "ID";

        /// <summary>
        /// Display name for OpenCAD document type value
        /// </summary>
        public const string OpenCADDocumentType = "OpenCADDocument";

        #endregion

        #region Section Headers

        /// <summary>
        /// Section header for layers
        /// </summary>
        public const string LayersSection = "--- Layers ---";

        /// <summary>
        /// Section header for children
        /// </summary>
        public const string ChildrenSection = "--- Children ---";

        /// <summary>
        /// Section header for geometry
        /// </summary>
        public const string GeometrySection = "--- Geometry ---";

        /// <summary>
        /// Property name for layer count
        /// </summary>
        public const string LayerCount = "Layer Count";

        /// <summary>
        /// Property name for total children count
        /// </summary>
        public const string TotalChildren = "Total Children";

        /// <summary>
        /// Property name for drawable objects count
        /// </summary>
        public const string DrawableObjects = "Drawable Objects";

        /// <summary>
        /// Section header for custom properties
        /// </summary>
        public const string CustomPropertiesSection = "--- Custom Properties ---";

        /// <summary>
        /// Property name for layer
        /// </summary>
        public const string Layer = "Layer";

        /// <summary>
        /// Display string for selection
        /// </summary>
        public const string Selection = "Selection";

        /// <summary>
        /// Display string for objects selected
        /// </summary>
        public const string ObjectsSelected = "objects selected";

        /// <summary>
        /// Display string for multiple values
        /// </summary>
        public const string MultipleValues = "<Multiple Values>";

        /// <summary>
        /// Property name for length
        /// </summary>
        public const string Length = "Length";

        #endregion

        #region Boolean Display Values

        /// <summary>
        /// Display string for true boolean values
        /// </summary>
        public const string TrueValue = "True";

        /// <summary>
        /// Display string for false boolean values
        /// </summary>
        public const string FalseValue = "False";

        #endregion

        #region Format Strings

        /// <summary>
        /// Format string for Point3D display (X, Y, Z)
        /// </summary>
        public const string Point3DFormat = "({0:F2}, {1:F2}, {2:F2})";

        /// <summary>
        /// Format string for ARGB color display
        /// </summary>
        public const string ColorARGBFormat = "ARGB({0}, {1}, {2}, {3})";

        /// <summary>
        /// Format string for double values
        /// </summary>
        public const string DoubleFormat = "F4";

        /// <summary>
        /// Format string for float values
        /// </summary>
        public const string FloatFormat = "F4";

        /// <summary>
        /// Format string for GUID with object type
        /// </summary>
        public const string GuidWithTypeFormat = "{0} ({1:D})";

        #endregion

        #region Debug/Log Messages

        /// <summary>
        /// Log message when properties are updated
        /// </summary>
        public const string PropertiesUpdatedFormat = "Properties updated for document: {0} ({1} properties)";

        /// <summary>
        /// Log message when property value is updated
        /// </summary>
        public const string PropertyValueUpdatedFormat = "Property '{0}' updated to: {1}";

        /// <summary>
        /// Error message when property update fails
        /// </summary>
        public const string PropertyUpdateErrorFormat = "Error updating property '{0}': {1}";

        /// <summary>
        /// Log message when layers are updated
        /// </summary>
        public const string LayersUpdatedFormat = "Layers updated for document: {0} ({1} layers)";

        /// <summary>
        /// Message format when layer is created
        /// </summary>
        public const string LayerCreatedFormat = "Layer '{0}' created";

        /// <summary>
        /// Message format when layer is deleted
        /// </summary>
        public const string LayerDeletedFormat = "Layer '{0}' deleted";

        /// <summary>
        /// Message format when current layer is changed
        /// </summary>
        public const string CurrentLayerChangedFormat = "Current layer set to '{0}'";

        /// <summary>
        /// Error message when trying to delete layer 0
        /// </summary>
        public const string CannotDeleteLayer0 = "Cannot delete layer '0'";

        /// <summary>
        /// Error message when trying to delete current layer
        /// </summary>
        public const string CannotDeleteCurrentLayer = "Cannot delete the current layer";

        #endregion

        #region Line Command Prompts

        /// <summary>
        /// Prompt for specifying start point with last point option
        /// </summary>
        public const string LineStartPointPromptWithLastPoint = "Specify start point (enter coordinates, click in viewport, or press Enter for last point {0:F3}, {1:F3}, {2:F3})";

        /// <summary>
        /// Prompt for specifying start point without last point
        /// </summary>
        public const string LineStartPointPrompt = "Specify start point (enter coordinates or click in viewport)";

        /// <summary>
        /// Prompt for specifying end point
        /// </summary>
        public const string LineEndPointPrompt = "Specify end point (enter coordinates or click in viewport)";

        /// <summary>
        /// Message displayed when start point is confirmed
        /// </summary>
        public const string LineStartPointConfirmed = "Start point: ({0:F3}, {1:F3}, {2:F3})";

        /// <summary>
        /// Message displayed when line is created
        /// </summary>
        public const string LineCreated = "Line created from ({0:F3}, {1:F3}, {2:F3}) to ({3:F3}, {4:F3}, {5:F3})";

        /// <summary>
        /// Error message for invalid point format
        /// </summary>
        public const string InvalidPointFormatWithExample = "Invalid point format. Use: x y z (e.g., {0}) or click in viewport";

        /// <summary>
        /// Undo action description for creating a line
        /// </summary>
        public const string UndoCreateLine = "Create Line ({0:F3}, {1:F3}, {2:F3}) to ({3:F3}, {4:F3}, {5:F3})";

        #endregion

        #region Line Command Debug Messages

        /// <summary>
        /// Debug message for LineCommand initialization
        /// </summary>
        public const string LineCommandInitialized = "LineCommand initialized";

        /// <summary>
        /// Debug message for LineCommand execute with viewport status
        /// </summary>
        public const string LineCommandExecuteViewportStatus = "LineCommand.Execute: viewport = {0}";

        /// <summary>
        /// Debug message when enabling mouse input
        /// </summary>
        public const string LineCommandEnablingMouseInput = "LineCommand: Enabling mouse input";

        /// <summary>
        /// Debug message when disabling mouse input
        /// </summary>
        public const string LineCommandDisablingMouseInput = "LineCommand: Disabling mouse input";

        /// <summary>
        /// Debug message when enabling preview mode
        /// </summary>
        public const string LineCommandEnablingPreviewMode = "LineCommand: Enabling preview mode for rubber band";

        /// <summary>
        /// Debug message when point is picked
        /// </summary>
        public const string LineCommandPointPicked = "LineCommand: Point picked ({0:F3}, {1:F3}, {2:F3})";

        /// <summary>
        /// Debug message when command is completed via mouse input
        /// </summary>
        public const string LineCommandCompletedViaMouse = "LineCommand: Command completed via mouse input";

        /// <summary>
        /// Debug message when preview point is updated
        /// </summary>
        public const string LineCommandPreviewPointUpdated = "LineCommand: Preview point updated to ({0:F3}, {1:F3}, {2:F3})";

        /// <summary>
        /// Viewport status: available
        /// </summary>
        public const string ViewportAvailable = "available";

        /// <summary>
        /// Viewport status: null
        /// </summary>
        public const string ViewportNull = "null";

        #endregion

        #region Erase Command Messages

        /// <summary>
        /// Debug message for EraseCommand initialization
        /// </summary>
        public const string EraseCommandInitialized = "EraseCommand initialized";

        /// <summary>
        /// Error message when no active viewport is available
        /// </summary>
        public const string NoActiveViewport = "No active viewport.";

        /// <summary>
        /// Error message when unable to access viewport
        /// </summary>
        public const string UnableToAccessViewport = "Unable to access viewport.";

        /// <summary>
        /// Prompt to select objects to erase
        /// </summary>
        public const string SelectObjectsToErasePrompt = "Select objects to erase (or press ESC to cancel):";

        /// <summary>
        /// Message when prompting user to select objects
        /// </summary>
        public const string SelectObjectsToEraseMessage = "Select objects to erase...";

        /// <summary>
        /// Message when no objects are selected and command is cancelled
        /// </summary>
        public const string NoObjectsSelectedCancelled = "No objects selected. Command cancelled.";

        /// <summary>
        /// Message when there are no objects to erase
        /// </summary>
        public const string NoObjectsToErase = "No objects to erase.";

        /// <summary>
        /// Error message when unable to erase objects due to missing document or viewport
        /// </summary>
        public const string UnableToEraseObjectsMissingContext = "Unable to erase objects: document or viewport not available.";

        /// <summary>
        /// Format string for undo action description when erasing objects
        /// </summary>
        public const string UndoEraseObjectsFormat = "Erase {0} object(s)";

        /// <summary>
        /// Format string for message displayed when objects are erased
        /// </summary>
        public const string ObjectsErasedFormat = "Erased {0} object(s).";

        /// <summary>
        /// Format string for message displayed when objects are erased without undo support
        /// </summary>
        public const string ObjectsErasedNoUndoFormat = "Erased {0} object(s) (no undo available).";

        #endregion

        #region Command Example Values

        /// <summary>
        /// Example coordinate for start point
        /// </summary>
        public const string ExampleStartPoint = "0 0 0";

        /// <summary>
        /// Example coordinate for end point
        /// </summary>
        public const string ExampleEndPoint = "5 5 0";

        #endregion

        #region PointInputHelper Prompts

        /// <summary>
        /// Prompt format with last point option
        /// </summary>
        public const string PromptWithLastPointFormat = "{0} (or press Enter for last point {1:F3}, {2:F3}, {3:F3}):";

        /// <summary>
        /// Prompt format for point input with viewport click option
        /// </summary>
        public const string PromptWithViewportFormat = "{0} (or click in viewport):";

        /// <summary>
        /// Message format when a point is selected
        /// </summary>
        public const string PointSelectedFormat = "Point selected: ({0:F3}, {1:F3}, {2:F3})";

        #endregion

        #region PointInputHelper Debug Messages

        /// <summary>
        /// Debug message when enabling point picking mode
        /// </summary>
        public const string PointInputHelperEnablingPickingMode = "PointInputHelper: Enabling point picking mode";

        /// <summary>
        /// Debug message when cleaning up point picking
        /// </summary>
        public const string PointInputHelperCleaningUp = "PointInputHelper: Cleaning up point picking";

        /// <summary>
        /// Debug message when cancel is called
        /// </summary>
        public const string PointInputHelperCancelCalled = "PointInputHelper: Cancel called";

        /// <summary>
        /// Debug message format when point is picked
        /// </summary>
        public const string PointInputHelperPointPickedFormat = "PointInputHelper: Point picked ({0:F3}, {1:F3}, {2:F3})";

        #endregion

        #region UI Tooltips

        /// <summary>
        /// Tooltip for new layer button
        /// </summary>
        public const string NewLayerTooltip = "Create New Layer";

        /// <summary>
        /// Tooltip for delete layer button
        /// </summary>
        public const string DeleteLayerTooltip = "Delete Selected Layer";

        /// <summary>
        /// Tooltip for set current layer button
        /// </summary>
        public const string SetCurrentLayerTooltip = "Set as Current Layer";

        /// <summary>
        /// Tooltip for refresh button
        /// </summary>
        public const string RefreshTooltip = "Refresh";

        #endregion

        #region UI Symbols

        /// <summary>
        /// Display string for current layer indicator
        /// </summary>
        public const string CurrentLayerIndicator = "✓";

        /// <summary>
        /// Display string for hidden layer indicator
        /// </summary>
        public const string HiddenLayerIndicator = "👁";

        /// <summary>
        /// Display string for locked layer indicator
        /// </summary>
        public const string LockedLayerIndicator = "🔒";

        /// <summary>
        /// Display string for layer visible (on)
        /// </summary>
        public const string LayerVisibleIndicator = "💡";

        /// <summary>
        /// Display string for layer hidden (off)
        /// </summary>
        public const string LayerHiddenIndicator = "◯";

        #endregion
    }
}