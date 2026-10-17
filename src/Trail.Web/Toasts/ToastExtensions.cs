using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Trail.Web.Toasts;

public static class ToastExtensions
{
    public const string TempDataKey = "__TrailToasts";
    public const string ViewDataKey = "Toasts";

    public static void SetToasts(ITempDataDictionary tempData, IReadOnlyList<Toast> toasts)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        ArgumentNullException.ThrowIfNull(toasts);

        if (toasts.Count > 0)
        {
            tempData[TempDataKey] = JsonSerializer.Serialize(toasts);
        }
    }

    public static void SetToasts(ViewDataDictionary viewData, IReadOnlyList<Toast> toasts)
    {
        ArgumentNullException.ThrowIfNull(viewData);
        ArgumentNullException.ThrowIfNull(toasts);

        if (toasts.Count > 0)
        {
            viewData[ViewDataKey] = toasts;
        }
    }

    public static IEnumerable<Toast> GetToasts(ViewDataDictionary viewData, ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(viewData);
        ArgumentNullException.ThrowIfNull(tempData);

        if (viewData.TryGetValue(ViewDataKey, out var viewToastsObj) && viewToastsObj is IEnumerable<Toast> viewToasts)
        {
            foreach (var toast in viewToasts)
            {
                yield return toast;
            }
        }

        if (tempData.TryGetValue(TempDataKey, out var tempToastsObj) && tempToastsObj is string json && !string.IsNullOrWhiteSpace(json))
        {
            List<Toast>? toasts = null;
            try
            {
                toasts = JsonSerializer.Deserialize<List<Toast>>(json);
            }
            catch
            {
                toasts = null;
            }

            if (toasts is not null)
            {
                foreach (var toast in toasts)
                {
                    yield return toast;
                }
            }
        }
    }
}
