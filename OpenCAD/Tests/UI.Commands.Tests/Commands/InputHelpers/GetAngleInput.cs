using Microsoft.VisualStudio.TestTools.UnitTesting;
using UI.Commands.InputHelpers;

namespace UI.Commands.Tests;

[TestClass]
public class GetAngleInputTests : InputHelperTestsBase
{
    [TestMethod]
    public async Task HandlesArbitraryAngleInput()
    {
        var inputHelper = new GetAngleInput(ContextMock.Object, ViewModel);
        inputHelper.AllowArbitraryInput = true;
        var result = await inputHelper.GetAngle("Enter angle:", false, null, null, default);
        var input = "45.5";
        var processed = inputHelper.ProcessKeyboardInput(input);
        Assert.IsTrue(processed, "Input should be processed.");
        //Assert.IsNotNull(inputHelper.LastInputResult, "LastInputResult should not be null.");
        //Assert.AreEqual(InputHelpers.InputResult.InputResultType.Arbitrary, inputHelper.LastInputResult?.ResultType, "ResultType should be Arbitrary.");
        //Assert.AreEqual(input, inputHelper.LastInputResult?.Keyword, "Keyword should match input.");
    }

}
