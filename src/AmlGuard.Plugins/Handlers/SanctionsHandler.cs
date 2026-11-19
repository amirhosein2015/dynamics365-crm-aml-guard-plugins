using System;
using AmlGuard.Plugins.Data;
using AmlGuard.Plugins.Domain;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins.Handlers
{
    // Screens one contact against the sanctions list. A match never throws: an exception would roll
    // back the transaction and the alert with it. Instead the contact is flagged and an alert is saved.
    public sealed class SanctionsHandler
    {
        private readonly AmlRepository _repository;
        private readonly ITracingService _tracing;

        public SanctionsHandler(IOrganizationService service, ITracingService tracing)
        {
            if (tracing == null)
            {
                throw new ArgumentNullException(nameof(tracing));
            }

            _repository = new AmlRepository(service);
            _tracing = tracing;
        }

        public void Handle(Guid contactId)
        {
            var contact = _repository.GetContact(contactId);

            if (NameNormalizer.NormalizeFullName(contact.FirstName, contact.LastName).Length == 0)
            {
                _tracing.Trace("Skipped: contact {0} has no name.", contactId);
                return;
            }

            var matcher = new SanctionsMatcher(_repository.GetSanctionedNames());
            var matchedName = matcher.FindMatch(contact.FirstName, contact.LastName);
            if (matchedName == null)
            {
                _tracing.Trace("No sanctions match for contact {0}.", contactId);
                return;
            }

            // Names are personal data, so the trace log records ids only. The alert carries the names.
            _tracing.Trace("Sanctions match for contact {0}.", contactId);

            if (contact.ReviewStatus != (int)ReviewStatus.UnderReview)
            {
                _repository.SetReviewStatus(contactId, ReviewStatus.UnderReview);
            }

            // One open alert per contact is enough; saving the name again must not pile up alerts.
            if (_repository.HasOpenAlert(contactId, AlertType.SanctionsMatch))
            {
                _tracing.Trace("An open sanctions alert already exists for contact {0}.", contactId);
                return;
            }

            var alertId = _repository.CreateAlert(
                contactId,
                AlertType.SanctionsMatch,
                AlertSeverity.Critical,
                SanctionsAlertText.Title(matchedName),
                SanctionsAlertText.Description(contact.FirstName, contact.LastName, matchedName));
            _tracing.Trace("Created sanctions alert {0}.", alertId);
        }
    }
}
