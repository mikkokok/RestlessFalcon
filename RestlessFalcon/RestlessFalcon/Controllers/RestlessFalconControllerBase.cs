using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Helpers.Impl;

namespace RestlessFalcon.Controllers
{
    public class RestlessFalconControllerBase : ControllerBase
    {
        protected readonly IDatabaseHelper _dbHelper;
        protected AuthKeyHelper _authKeyHelper;
        protected Logger _logger;

        public RestlessFalconControllerBase(IDatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
            _authKeyHelper = new AuthKeyHelper(_dbHelper, _dbHelper.Config);
            _logger = new Logger();
        }
    }
}