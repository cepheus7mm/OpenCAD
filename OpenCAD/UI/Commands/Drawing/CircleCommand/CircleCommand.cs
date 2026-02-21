using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Drawing.CircleCommand.Modes;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing.CircleCommand
{
    /// <summary>
    /// Command to create an Circle by specifying center, start point (defines radius and start angle),
    /// and end point (defines end angle)
    /// </summary>
    [InputCommand("circle", "Create a circle", "ci")]
    public class CircleCommand : CommandBase
    {
        private ICircleCreationMode _mode;

        public override async Task Execute()
        {
            var document = Context?.GetDocument();
            _mode = new CenterRadiusMode(document);
            BasePoint = Point3D.NotAPoint;
            bool cancel = false;

            while (!_mode.IsComplete  && !cancel)
            {
                BasePoint = _mode.GetBasePoint();
                var input = _mode.UserInputType switch
                {
                    UserInputType.Point => await GetPoint(_mode.Prompt, BasePoint, _mode.Keywords),
                    UserInputType.Distance => await GetDistance(_mode.Prompt, defaultValue: document.LastDistance),
                    _ => new InputResult { ResultType = InputResult.InputResultType.Cancel }
                };

                if (input.IsCancel)
                {
                    cancel = true;
                }

                if (input.IsKeyword)
                {
                    _mode = SwitchMode(_mode, input.Keyword!, document);
                    continue;
                }

                if (input.IsPoint)
                {
                    _mode.SetPoint(input.Point!.Value);
                    UpdatePreview();
                }

                if (input.IsDouble)
                {
                    document.LastDistance = input.DoubleValue;
                    _mode.SetDistance(input.DoubleValue);
                }
            }

            if (!cancel)
            {
                var circle = _mode.CreateCircle();
                CreateObject(circle);
            }
            else
            {
                Cancel();
            }
            CommandCompleted();
        }

        private ICircleCreationMode SwitchMode(ICircleCreationMode currentMode, string keyword, OpenCADDocument document)
        {
            return keyword.ToUpperInvariant() switch
            {
                "RAD" => new CenterRadiusMode(document),
                "DIA" => new CenterDiameterMode(document),
                "2PT" => new TwoPointCircleMode(document),
                "3PT" => new ThreePointCircleMode(document),
                _ => currentMode
            };
        }

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            yield return _mode.GetPreview(TargetPoint);
        }
    }
}