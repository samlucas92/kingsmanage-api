using System.Linq.Expressions;
using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo;

public sealed class TenantMongoScope
{
	private readonly ITenantContext tenantContext;

	public TenantMongoScope(ITenantContext tenantContext)
	{
		this.tenantContext = tenantContext;
	}

	public FilterDefinition<T> Filter<T>() where T : ITenantOwned
	{
		return Builders<T>.Filter.Eq(item => item.OrganizationId, tenantContext.OrganizationId) &
			Builders<T>.Filter.Eq(item => item.ClubId, tenantContext.ClubId);
	}

	public FilterDefinition<T> Filter<T>(Expression<Func<T, bool>> filter) where T : ITenantOwned
	{
		return Filter<T>() & Builders<T>.Filter.Where(filter);
	}

	public T Assign<T>(T item) where T : ITenantOwned
	{
		item.OrganizationId = tenantContext.OrganizationId;
		item.ClubId = tenantContext.ClubId;
		return item;
	}
}
