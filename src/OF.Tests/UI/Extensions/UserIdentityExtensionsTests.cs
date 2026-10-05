using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using OF.Data.Database;
using OF.UI.Identity;

namespace OF.Tests.UI.Extensions
{
    public class UserIdentityExtensionsTests
    {
        [Fact]
        public void GetDivisionsForUser_Returns_Cookie_Divisions_Valid_For_User()
        {
            // Arrange
            IRequestCookieCollection cookie = new TestCookieCollection("user_divisions", " 100, 200, 300");
            User id = new (){ Division = "300 ,400 ,500 " };

            // Act
            string[] divisions = id.GetDivisionsForUser(cookie);

            // Assert
            divisions.Should().NotBeNull();
            divisions.Should().HaveCount(1);
            divisions[0].Should().Be("300");
        }

        [Fact]
        public void GetDivisionsForUser_Returns_User_Divisions_Cookie_Is_Null()
        {
            // Arrange
            User id = new() { Division = "300,400,500" };

            // Act
            string[] divisions = id.GetDivisionsForUser(null);

            // Assert
            divisions.Should().NotBeNull();
            divisions.Should().HaveCount(3);
        }

        [Fact]
        public void GetDivisionsForUser_Returns_EmptyList_If_User_Divisions_IsNull()
        {
            // Arrange
            User id = new();

            // Act
            string[] divisions = id.GetDivisionsForUser(null);

            // Assert
            divisions.Should().NotBeNull();
            divisions.Should().HaveCount(0);
        }

        private class TestCookieCollection : IRequestCookieCollection
        {
            private readonly Dictionary<string, string> cookies = new();
            public TestCookieCollection(string key, string value)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    cookies.Add(key, value);
                }
            }

            public string? this[string key] => cookies[key];

            public int Count => cookies.Count;

            public ICollection<string> Keys => cookies.Keys;

            public bool ContainsKey(string key) => cookies.ContainsKey(key);

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator() 
                => cookies.GetEnumerator();

            public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
                => cookies.TryGetValue(key, out value);

            IEnumerator IEnumerable.GetEnumerator() 
                => cookies.GetEnumerator();
        }
    }
}
