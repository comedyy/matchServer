namespace  Game
{
    public enum LevelType
    {
        Main,
        Challenge
    }

    public class ListPool<T> where T : new()
    {
        public static List<T> Get()
        {
            return new List<T>();
        }
    }
}
 