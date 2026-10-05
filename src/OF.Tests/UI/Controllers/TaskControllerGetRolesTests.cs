using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Common;
using OF.UI.Controllers;
using OF.UI.Database;
using OF.UI.Identity;

namespace OF.Tests.UI.Controllers;

public class TaskControllerGetRolesTests
{
    [Fact]
    public void GetRoles_IncludesFrontendPreviewRole()
    {
        var subject = new TaskController(
            Mock.Of<IDataRepository>(),
            Mock.Of<IUserIdentity>(),
            Mock.Of<IHttpContextAccessor>());

        var result = subject.GetRoles().Result.Should().BeOfType<ObjectResult>().Subject;
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain(Constants.Roles.NewFrontendPreview);
    }
}
