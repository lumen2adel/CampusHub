using CampusHub.Classes.Group_Posts;
using CampusHub.Classes.UserAccount;
using CampusHub.Data;
using Microsoft.EntityFrameworkCore;


namespace CampusHub.Services
{
    public class SearchService
    {
        private readonly DataContext _dbContext;

        public SearchService(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Post>> SearchPostsAsync(string query)
        {
            return await _dbContext.Posts
                 .FromSqlRaw(@"
                 SELECT * FROM ""Posts"" 
                 WHERE search_vector @@ plainto_tsquery({0}) 
                 OR ""Description"" ILIKE '%' || {0} || '%' 
                 OR ""Description"" % {0}
                 ORDER BY ""CreatedAt"" DESC 
                 LIMIT 20;", query)

                .ToListAsync();
        }

        public async Task<List<Group>> SearchGroupsAsync(string query)
        {
            return await _dbContext.Groups
                .FromSqlRaw(@"
            SELECT * FROM ""Groups"" 
            WHERE search_vector @@ plainto_tsquery({0}) 
            OR ""Name"" ILIKE '%' || {0} || '%' 
            OR ""Description"" ILIKE '%' || {0} || '%' 
            OR ""Name"" % {0}
            ORDER BY ""CreatedAt"" DESC 
            LIMIT 20;", query)
                .ToListAsync();
        }

        public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
        {
            // Raw SQL only filters; projecting to UserSearchResult means credential columns are
            // never selected. E-mail is not searchable, so search cannot confirm who is registered.
            return await _dbContext.Users
                .FromSqlRaw(@"
            SELECT * FROM ""Users""
            WHERE search_vector @@ plainto_tsquery({0})
            OR ""FirstName"" ILIKE '%' || {0} || '%'
            OR ""LastName"" ILIKE '%' || {0} || '%'
            OR ""FirstName"" % {0}", query)
                .OrderBy(u => u.FirstName)
                .Take(20)
                .Select(u => new UserSearchResult(u.Id, u.FirstName, u.LastName))
                .ToListAsync();
        }

    }
}
