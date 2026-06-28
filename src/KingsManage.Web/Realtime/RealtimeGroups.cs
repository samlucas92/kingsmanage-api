namespace KingsManage.Web.Realtime;

public static class RealtimeGroups
{
	public static string Organization(Guid organizationId) =>
		$"organization:{organizationId:N}";

	public static string Club(Guid organizationId, Guid clubId) =>
		$"club:{organizationId:N}:{clubId:N}";

	public static string User(Guid organizationId, Guid clubId, Guid userId) =>
		$"user:{organizationId:N}:{clubId:N}:{userId:N}";
}
