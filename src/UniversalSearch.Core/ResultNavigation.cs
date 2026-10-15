namespace UniversalSearch.Core;

public static class ResultNavigation
{
    public static int Next(int selected, int count, bool down, bool fromSearch)
    {
        if (count <= 0) return -1;
        if (fromSearch) return down ? 0 : count - 1;
        return Math.Clamp(selected + (down ? 1 : -1), 0, count - 1);
    }
}
