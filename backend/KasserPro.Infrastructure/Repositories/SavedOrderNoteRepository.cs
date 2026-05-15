namespace KasserPro.Infrastructure.Repositories;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Infrastructure.Data;

public class SavedOrderNoteRepository : GenericRepository<SavedOrderNote>, ISavedOrderNoteRepository
{
    public SavedOrderNoteRepository(AppDbContext context) : base(context)
    {
    }
}
