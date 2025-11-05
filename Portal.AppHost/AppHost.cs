var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Portal_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithEnvironment("TEST_USER", "testuser1")
    .WithEnvironment("TEST_ROLES", "admin")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
