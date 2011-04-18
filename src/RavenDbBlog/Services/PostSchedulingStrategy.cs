using System;
using System.Linq;
using Raven.Client;
using RavenDbBlog.Core.Models;

namespace RavenDbBlog.Services
{
	public class PostSchedulingStrategy
	{
		private readonly IDocumentSession _session;
	    private readonly DateTimeOffset? _currentDate;

	    public PostSchedulingStrategy(IDocumentSession session, DateTimeOffset? currentDate)
        {
            _session = session;
            _currentDate = currentDate;
        }

	    /// <summary>
		/// The rules are simple:
		/// * If there is no set date, schedule in at the end of the queue, but on a Monday - Friday 
		/// * If there is a set date, move all the posts from that day one day forward
		///  * only to Monday - Friday
		///  * don't touch posts that are marked with SkipAutoReschedule = true
		/// * If we are moving a current post, we need to:
		///  * Check if there is a post in the day we shifted the edited post to, if so, switch them
		///  * Else, trim all the holes in the schedule
		/// </summary>
        public DateTimeOffset Schedule(DateTimeOffset? requestedDate = null)
		{
            if (requestedDate == null)
            {
                var date = GetLastPostOnSchedule();
                date.AddDays(1);
                date = PutScheduleInWorkdaysOnly(date);
                return date;
            }

	        var postsQuery = from p in _session.Query<Post>()
	                         where p.PublishAt > requestedDate && p.SkipAutoReschedule == false
	                         orderby p.PublishAt
	                         select p;
	        var rescheduleDate = requestedDate.Value;
	        foreach (var post in postsQuery)
	        {
	            rescheduleDate = PutScheduleInWorkdaysOnly(rescheduleDate.AddDays(1));
                post.PublishAt = NewDate(rescheduleDate, post.PublishAt);
	        }

	        return DateTimeOffset.Now;
		}

	    private static DateTimeOffset NewDate(DateTimeOffset date, DateTimeOffset time)
	    {
            return new DateTimeOffset(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Millisecond, time.Offset);
	    }

	    private DateTimeOffset PutScheduleInWorkdaysOnly(DateTimeOffset date)
	    {
            // TODO: Use CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek
            if (date.DayOfWeek == DayOfWeek.Saturday)
                return date.AddDays(2);

            if (date.DayOfWeek == DayOfWeek.Sunday)
                return date.AddDays(1);

	        return date;
	    }

	    private DateTimeOffset GetLastPostOnSchedule()
	    {
	        var post = _session.Query<Post>()
	            .OrderByDescending(p => p.PublishAt)
	            .FirstOrDefault();

	        if (post == null)
                return DateTimeOffset.Now;
	            
	        return post.PublishAt;
	    }
	}
}