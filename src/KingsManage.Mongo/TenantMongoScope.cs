using System.Linq.Expressions;
using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo;

public sealed class TenantMongoScope
{
	private readonly ITenantContext _tenantContext;

	public TenantMongoScope(ITenantContext tenantContext)
	{
		_tenantContext = tenantContext;
	}

	public FilterDefinition<T> Filter<T>() where T : ITenantOwned
	{
		return Builders<T>.Filter.Eq(item => item.OrganizationId, _tenantContext.OrganizationId) &
			Builders<T>.Filter.Eq(item => item.ClubId, _tenantContext.ClubId);
	}

	public FilterDefinition<T> Filter<T>(Expression<Func<T, bool>> filter) where T : ITenantOwned
	{
		return Filter<T>() & Builders<T>.Filter.Where(filter);
	}

	public T Assign<T>(T item) where T : ITenantOwned
	{
		item.OrganizationId = _tenantContext.OrganizationId;
		item.ClubId = _tenantContext.ClubId;
		return item;
	}
}
