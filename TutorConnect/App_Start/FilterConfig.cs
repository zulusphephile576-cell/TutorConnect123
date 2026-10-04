using System.Web.Mvc;
using TutorConnect.Filters;

namespace TutorConnect
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new RequestTimingAttribute());
            filters.Add(new FriendlyErrorAttribute());
            filters.Add(new DatabaseReadyAttribute());
        }
    }
}
