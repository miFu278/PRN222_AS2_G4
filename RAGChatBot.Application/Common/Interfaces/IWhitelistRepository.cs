using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IWhitelistRepository
    {
        Task<WhitelistEmail?> GetByIdAsync(Guid id);
        Task<WhitelistEmail?> GetByEmailAsync(string email);
        Task<IEnumerable<WhitelistEmail>> GetAllAsync();
        Task AddAsync(WhitelistEmail entity);
        Task DeleteAsync(WhitelistEmail entity);
        Task SaveChangesAsync();
    }
}
