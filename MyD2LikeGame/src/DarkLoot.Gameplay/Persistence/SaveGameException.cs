namespace DarkLoot.Gameplay.Persistence;

public sealed class SaveGameException : Exception
{
    public SaveGameException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
