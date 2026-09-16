using System;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services;

public class ActivityClassificationService : IActivityClassificationService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IProductivityClassificationRepository _productivityClassificationRepository;

    public ActivityClassificationService(
        ITeamRepository teamRepository,
        IProductivityClassificationRepository productivityClassificationRepository)
    {
        _teamRepository = teamRepository;
        _productivityClassificationRepository = productivityClassificationRepository;
    }

    public async Task<ActivityCategory> ClassifyAsync(
        string applicationName,
        string? url,
        Guid userId,
        Guid organizationId)
    {
        var team = await _teamRepository.GetByUserIdAsync(userId, organizationId);

        if (team != null)
        {
            var classifications =
                await _productivityClassificationRepository.GetByTeamAsync(organizationId, team.Id);

            /*
             * Primeiro tenta classificar pelo site.
             *
             * Exemplo:
             * URL recebida: https://youtube.com/watch?v=123
             * Identifier salvo: site:youtube.com
             */
            if (!string.IsNullOrWhiteSpace(url) &&
                Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host
                    .Replace("www.", "", StringComparison.OrdinalIgnoreCase)
                    .ToLowerInvariant();

                var siteClassification = classifications.FirstOrDefault(item =>
                {
                    if (item.ItemType != ProductivityItemType.Site)
                        return false;

                    var identifier = item.Identifier
                        .Replace("site:", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("www.", "", StringComparison.OrdinalIgnoreCase)
                        .Trim()
                        .ToLowerInvariant();

                    return host == identifier || host.EndsWith($".{identifier}");
                });

                if (siteClassification != null)
                    return siteClassification.Category;
            }

            /*
             * Se não encontrou um site, tenta classificar pelo aplicativo.
             *
             * Exemplo:
             * ApplicationName recebido: chrome
             * Identifier salvo: app:chrome
             */
            if (!string.IsNullOrWhiteSpace(applicationName))
            {
                var normalizedApplication = applicationName.Trim().ToLowerInvariant();

                var applicationClassification = classifications.FirstOrDefault(item =>
                {
                    if (item.ItemType != ProductivityItemType.Application)
                        return false;

                    var identifier = item.Identifier
                        .Replace("app:", "", StringComparison.OrdinalIgnoreCase)
                        .Trim()
                        .ToLowerInvariant();

                    return normalizedApplication == identifier ||
                           normalizedApplication.Contains(identifier) ||
                           identifier.Contains(normalizedApplication);
                });

                if (applicationClassification != null)
                    return applicationClassification.Category;
            }
        }

        /*
         * Regras padrão usadas quando:
         * - o usuário não pertence a uma equipe;
         * - a equipe não possui uma classificação cadastrada;
         * - o site ou aplicativo não foi encontrado.
         */
        if (string.IsNullOrWhiteSpace(applicationName))
            return ActivityCategory.Neutral;

        var app = applicationName.ToLowerInvariant();

        if (app.Contains("spotify") || app.Contains("youtube"))
            return ActivityCategory.Unproductive;

        if (app.Contains("chrome") || app.Contains("edge") || app.Contains("firefox"))
            return ActivityCategory.Productive;

        return ActivityCategory.Neutral;
    }
}
