using System.Text.RegularExpressions;

namespace Crypto.Tests;

public class UtilsTests
{
    [Test]
    public async Task RandomBase32String_ProducesBase32AlphabetOnlyAsync()
    {
        var value = Utils.RandomBase32String(8);

        await Assert.That(Regex.IsMatch(value, "^[a-z2-7]+$")).IsTrue();
    }

    [Test]
    public async Task RandomBase32String_EncodesRequestedByteCountAsync()
    {
        var value = Utils.RandomBase32String(8);

        await Assert.That(value.Length).IsEqualTo(13);
    }

    [Test]
    public async Task RandomBase32String_DiffersBetweenCallsAsync()
    {
        var value1 = Utils.RandomBase32String(8);
        var value2 = Utils.RandomBase32String(8);

        await Assert.That(value1).IsNotEqualTo(value2);
    }
}
