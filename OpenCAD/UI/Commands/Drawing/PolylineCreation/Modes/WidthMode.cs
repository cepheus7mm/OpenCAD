using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class WidthMode : PolylineCreationModeBase
    {
        private double? _startWidth;
        private double? _endWidth;

        public override string Prompt =>
            !_startWidth.HasValue
                ? $"Specify starting width"
                : $"Specify ending width";

        private readonly PolylineCommand _command;

        public override UserInputType UserInputType => UserInputType.Distance;

        public override bool AllowArbitraryInput => true;

        public WidthMode(PolylineCommand command)
        {
            _command = command;
        }

        public override string[] Keywords =>
            Array.Empty<string>();

        public override bool AllowLastPoint => false;

        public override bool IsComplete =>
            _startWidth.HasValue && _endWidth.HasValue;

        public override void SetPoint(Point3D point)
        {
            // WidthMode does not accept points
        }

        public override void SetDouble(double value)
        {
            if (!_startWidth.HasValue)
                _startWidth = value;
            else if (!_endWidth.HasValue)
                _endWidth = value;
        }

        public override object? GetDefaultValue()
        {     
            if (!_startWidth.HasValue)
                return _command.CurrentEndWidth;
            else if (!_endWidth.HasValue)
                return _startWidth.Value;
            return null;
        }

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            // Apply the new widths
            command.SetCurrentWidths(_startWidth!.Value, _endWidth!.Value);

            // Return to NextPointMode
            return new NextPointMode(command.LastPoint!.Value);
        }
    }
}
