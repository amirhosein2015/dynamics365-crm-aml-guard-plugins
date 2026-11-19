namespace AmlGuard.Plugins.Data
{
    // The few contact columns the sanctions check needs, read fresh from Dataverse.
    internal sealed class ContactSnapshot
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int? ReviewStatus { get; set; }
    }
}
