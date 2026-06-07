namespace FamilyMoney.Utils;

public static class ImageDataHelper
{
    public static byte[]? ToByteArray(Stream? stream)
    {
        if (stream == null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public static bool AreEqual(byte[]? left, byte[]? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.AsSpan().SequenceEqual(right);
    }
}
