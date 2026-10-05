using OF.UI.Database;
using OF.Data.Database;
using Microsoft.AspNetCore.Http;

namespace OF.UI.Identity
{
    public class UserIdentity : IUserIdentity
    {
        private readonly IDataRepository _repository;
        private readonly IHttpContextAccessor _context;
        private User _user;

        public User GetIdentity()
        {
            if (_user != null)
            {
                return _user;
            }

            if (_context.HttpContext != null && _context.HttpContext.User != null && _context.HttpContext.User.Identity != null)
            {
                var userName = _context.HttpContext.User.Identity.Name;

                if (!String.IsNullOrEmpty(userName))
                {
                    _user = _repository.GetUser(userName);
                    return _user;
                }
            }

            return null;
        }

        public UserIdentity(IHttpContextAccessor context, IDataRepository repository)
        {
            _repository = repository;
            _context = context;
        }

    }
}
