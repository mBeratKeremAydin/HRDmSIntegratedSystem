namespace DMS.Mvc.Models 
{
    public class SystemLogViewModel
    {
        public string Status { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangeDate { get; set; }
        public string DocumentTitle { get; set; } 
    }
}