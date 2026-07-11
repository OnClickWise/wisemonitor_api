using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class KeyboardService : IKeyboardService
    {
        private readonly IKeyboardRepository _repository;

        public KeyboardService(IKeyboardRepository repository)
        {
            _repository = repository;
        }

        public async Task ProcessKeyboardEventAsync(
            KeyboardEventCreateDTO dto,
            Guid userId,
            Guid organizationId)
        {
            var result = KeyboardProductivityHelper.Calculate(dto);

            var session = new KeyboardSession
            {
                Id = dto.SessionId,
                UserId = userId,
                OrganizationId = organizationId,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                Application = dto.Application,
                TotalKeystrokes = dto.Metrics.TotalKeystrokes,
                LettersCount = dto.Metrics.Letters,
                WordsCount = dto.Metrics.Words,
                NumbersCount = dto.Metrics.Numbers,
                SymbolsCount = dto.Metrics.Symbols,
                ProductivityScore = result.Score,
                Classification = result.Classification
            };

            await _repository.CreateAsync(session);
        }

        public Task<KeyboardSession> GetByIdAsync(Guid id, Guid userId)
            => _repository.GetByIdAsync(id, userId);

        public Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
            => _repository.GetHistoryAsync(userId, start, end);

        public Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
            => _repository.GetSummaryAsync(userId, start, end);

        public async Task<bool> UpdateAsync(
            Guid id, KeyboardEventUpdateDTO dto, Guid userId)
        {
            var session = await _repository.GetByIdAsync(id, userId);
            if (session == null) return false;

            session.EndAt = dto.EndAt;
            session.TotalKeystrokes = dto.TotalKeystrokes;
            session.LettersCount = dto.LettersCount;
            session.WordsCount = dto.WordsCount;
            session.NumbersCount = dto.NumbersCount;
            session.SymbolsCount = dto.SymbolsCount;

            await _repository.UpdateAsync(session);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid userId)
        {
            var session = await _repository.GetByIdAsync(id, userId);
            if (session == null) return false;

            await _repository.DeleteAsync(session);
            return true;
        }
    }
}