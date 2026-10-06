using CampusHub.IntegrationTests.Infrastructure;

// One API + PostgreSQL container for the whole test run; tests receive it through their constructor
[assembly: AssemblyFixture(typeof(CampusHubApiFactory))]
