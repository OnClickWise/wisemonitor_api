using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class ProductivityClassificationRepository
        : IProductivityClassificationRepository
    {
        private readonly AppDbContext _context;

        public ProductivityClassificationRepository(
            AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProductivityClassification>>
            GetByTeamAsync(
                Guid organizationId,
                Guid teamId)
        {
            return await _context.ProductivityClassifications
                .Where(c =>
                    c.OrganizationId == organizationId &&
                    c.TeamId == teamId)
                .AsNoTracking()
                .OrderBy(c => c.DisplayName)
                .ToListAsync();
        }

        public async Task SaveAsync(
            IEnumerable<ProductivityClassification> items)
        {
            var list = items.ToList();

            if (list.Count == 0)
            {
                return;
            }

            var organizationId = list[0].OrganizationId;
            var teamId = list[0].TeamId;

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    var existing =
                        await _context.ProductivityClassifications
                            .Where(c =>
                                c.OrganizationId == organizationId &&
                                c.TeamId == teamId)
                            .ToListAsync();

                    if (existing.Count > 0)
                    {
                        _context.ProductivityClassifications
                            .RemoveRange(existing);

                        /*
                         * Confirma a exclusão antes de inserir os novos
                         * registros para evitar conflito com a chave única:
                         *
                         * OrganizationId + TeamId + Identifier + ItemType
                         */
                        await _context.SaveChangesAsync();
                    }

                    await _context.ProductivityClassifications
                        .AddRangeAsync(list);

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }
}
