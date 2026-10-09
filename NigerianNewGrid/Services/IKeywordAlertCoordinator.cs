using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface IKeywordAlertCoordinator
{
    void EvaluateKeywordAlerts(IReadOnlyList<BriefingCategory> categories);
}
