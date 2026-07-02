using KingsManage;

namespace KingsManage.Web.Services;

public sealed class FileLifecycleBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly FileLifecycleSettings _settings;
	private readonly ILogger<FileLifecycleBackgroundService> _logger;

	public FileLifecycleBackgroundService(
		IServiceScopeFactory scopeFactory,
		FileLifecycleSettings settings,
		ILogger<FileLifecycleBackgroundService> logger
	)
	{
		_scopeFactory = scopeFactory;
		_settings = settings;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var interval = TimeSpan.FromMinutes(
			Math.Max(1, _settings.CleanupIntervalMinutes)
		);
		using var timer = new PeriodicTimer(interval);

		await RunMaintenanceAsync(stoppingToken);

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			await RunMaintenanceAsync(stoppingToken);
		}
	}

	private async Task RunMaintenanceAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var lifecycle = scope.ServiceProvider.GetRequiredService<IFileLifecycleService>();
			var result = await lifecycle.RunMaintenanceAsync(
				DateTime.UtcNow,
				cancellationToken
			);

			if (
				result.AbandonedReferencesDeleted > 0 ||
				result.ReferenceCountsReconciled > 0 ||
				result.ExternalObjectsDeleted > 0 ||
				result.DeletionFailures > 0
			)
			{
				_logger.LogInformation(
					"File lifecycle maintenance completed: {AbandonedReferences} abandoned references, {ReconciledObjects} reconciled objects, {DeletedObjects} deleted objects, {DeletionFailures} deletion failures.",
					result.AbandonedReferencesDeleted,
					result.ReferenceCountsReconciled,
					result.ExternalObjectsDeleted,
					result.DeletionFailures
				);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "File lifecycle maintenance failed.");
		}
	}
}
