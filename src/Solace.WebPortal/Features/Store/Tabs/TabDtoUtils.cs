using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Features.Store.Tabs;

public static class TabDtoUtils
{
    public static bool IsValid(TabDto? tab)
    {
        if (tab is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(tab.TabId) || string.IsNullOrWhiteSpace(tab.TabTitle) || string.IsNullOrWhiteSpace(tab.TabIcon))
        {
            return false;
        }

        foreach (var sq in tab.ScreenLayoutQueries)
        {
            if (!Enum.IsDefined(sq.ColumnType))
            {
                return false;
            }
        }

        return true;
    }
}
