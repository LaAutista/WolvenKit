using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WolvenKit.Core.Interfaces;
using WolvenKit.Modkit.Scripting;

namespace WolvenKit.UnitTests;

[TestClass]
public class ScriptServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_LoadsNativeV8AndCallsHost()
    {
        var result = 0;
        var service = new ScriptService(Mock.Of<ILoggerService>());
        await service.ExecuteAsync("report(6 * 7);", new Dictionary<string, object>
        {
            ["report"] = new Action<int>(value => result = value)
        });
        Assert.AreEqual(42, result);
        Assert.IsFalse(service.IsRunning);
    }
}
