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
            Cancel
        }

        public enum ProcessingResultType
        {
            None,
            Completed,
            RequiresMoreInput
        }

        public double DoubleValue { get; set; }
        public Point3D? Point { get; set; }
        public string? Keyword { get; set; }
        public ProcessingResultType ProcessingResult { get; set; } = ProcessingResultType.None;

        public InputResultType ResultType
        {
            get => _resultType;
            set => _resultType = value;
        }
    }
}
