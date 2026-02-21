using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.InputHelpers
{
    public class InputResult
    {
        private InputResultType _resultType = InputResultType.None;
        public enum InputResultType
        {
            None,
            Point,
            Double,
            Keyword,
            Arbitrary,
            ProcessingResult,
            ObjectAndPoint,
            Hover,
            Cancel,
            Default
        }

        public enum ProcessingResultType
        {
            None,
            Completed,
            RequiresMoreInput,
            Invalid
        }

        public double DoubleValue { get; set; } = double.NaN;
        public Point3D? Point { get; set; } = null;
        public string? Arbitrary { get; set; } = null;
        public string? Keyword { get; set; } = null;
        public OpenCADObject? Object { get; set; } = null;
        public ProcessingResultType ProcessingResult { get; set; } = ProcessingResultType.None;

        public InputResultType ResultType
        {
            get => _resultType;
            set => _resultType = value;
        }

        public bool IsPoint => Point.HasValue;

        public bool IsDouble => !double.IsNaN(DoubleValue) && ResultType == InputResultType.Double;

        public bool IsKeyword => !string.IsNullOrEmpty(Keyword);

        public bool IsObject => Object != null;

        public bool IsCancel => ResultType == InputResultType.Cancel;

        public bool IsArbitrary => ResultType == InputResultType.Arbitrary && !string.IsNullOrEmpty(Arbitrary);

        public bool IsDefault => ResultType == InputResultType.Default;

        public static InputResult FromPoint(Point3D p) =>
            new InputResult
            {
                Point = p,
                ResultType = InputResultType.Point,
                ProcessingResult = ProcessingResultType.Completed
            };

        public static InputResult FromDouble(double d) =>
            new InputResult
            {
                DoubleValue = d,
                ResultType = InputResultType.Double,
                ProcessingResult = ProcessingResultType.Completed
            };

        public static InputResult FromArbitrary(string input) =>
            new InputResult
            {
                Arbitrary = input,
                ResultType = InputResultType.Arbitrary,
                ProcessingResult = ProcessingResultType.Completed
            };

        public static InputResult FromKeyword(string keyword) =>
            new InputResult
            {
                Keyword = keyword,
                ResultType = InputResultType.Keyword,
                ProcessingResult = ProcessingResultType.Completed
            };

        public static InputResult FromObjectAndPoint(OpenCADObject obj, Point3D p) =>
            new InputResult
            {
                Object = obj,
                Point = p,
                ResultType = InputResultType.ObjectAndPoint,
                ProcessingResult = ProcessingResultType.Completed
            };
    
        public static InputResult Cancel =>
            new InputResult
            {
                ResultType = InputResultType.Cancel,
                ProcessingResult = ProcessingResultType.None
            };

        public static InputResult BadDouble =>
            new InputResult
            {
                DoubleValue = double.NaN,
                ResultType = InputResultType.None,
                ProcessingResult = ProcessingResultType.Invalid
            };

        public static InputResult Default =>
            new InputResult
            {
                ResultType = InputResultType.Default,
                ProcessingResult = ProcessingResultType.Completed
            };
    }
}
