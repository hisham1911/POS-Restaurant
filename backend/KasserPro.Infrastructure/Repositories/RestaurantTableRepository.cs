namespace KasserPro.Infrastructure.Repositories;

using KasserPro.Application.Common.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Infrastructure.Data;

public class RestaurantTableRepository : GenericRepository<RestaurantTable>, IRestaurantTableRepository
{
    public RestaurantTableRepository(AppDbContext context) : base(context)
    {
    }
}
