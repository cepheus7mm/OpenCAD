using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public readonly struct MeasurementResult
    {
        public double Value { get; }
        public readonly OpenCADDocument.UnitFormatType UnitFormatType { get; }

        public MeasurementResult(double value, OpenCADDocument.UnitFormatType unitSymbol)
        {
            Value = value;
            UnitFormatType = unitSymbol;
        }

        public string FormattedResult(OpenCADDocument document)
        {
            return document.ValueToString(value: Value, type: UnitFormatType);
        }
    }
}
