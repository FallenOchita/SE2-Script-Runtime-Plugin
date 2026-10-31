Api.Log("Hello from the SE2 Script Runtime example script!");
while (!Api.CancellationToken.IsCancellationRequested)
{
    await Api.Delay(TimeSpan.FromSeconds(10));
    Api.Log("Example script is still running.");
}
