using NUnit.Framework;
using UnityToFigma.Editor.Utils;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaTokenLogMessagesTests
    {
        [Test]
        public void PersonalAccessTokenSavedMessage_DoesNotContainRepresentativeSecret()
        {
            const string fakeToken = "figd_ThisMustNeverAppearInLogMessage_XYZ123";
            var message = FigmaTokenLogMessages.GetPersonalAccessTokenSavedMessage();
            Assert.That(message, Does.Not.Contain(fakeToken));
            Assert.That(message, Does.Not.Contain("figd_"));
        }

        [Test]
        public void PersonalAccessTokenSavedMessage_IsFixedSafeConstant()
        {
            Assert.That(
                FigmaTokenLogMessages.GetPersonalAccessTokenSavedMessage(),
                Is.EqualTo(FigmaTokenLogMessages.PersonalAccessTokenSavedToPreferences));
        }
    }
}
