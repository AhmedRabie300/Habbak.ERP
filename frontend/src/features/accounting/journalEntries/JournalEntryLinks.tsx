import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

interface JournalEntryLinksProps {
  journalEntryId?: number | null;
  journalEntryNumber?: string | null;
  reversalJournalEntryId?: number | null;
  reversalJournalEntryNumber?: string | null;
}

/** A posted document's journal entry, and the reversing one if it was cancelled — shown in its totals row. */
export function JournalEntryLinks({ journalEntryId, journalEntryNumber, reversalJournalEntryId, reversalJournalEntryNumber }: JournalEntryLinksProps) {
  const { t } = useTranslation();
  return (
    <>
      {journalEntryId && (
        <span>
          {t('journalEntryLinks.entry')}: <Link to={`/accounting/journal-entries/${journalEntryId}`}>{journalEntryNumber}</Link>
        </span>
      )}
      {reversalJournalEntryId && (
        <span>
          {t('journalEntryLinks.reversal')}: <Link to={`/accounting/journal-entries/${reversalJournalEntryId}`}>{reversalJournalEntryNumber}</Link>
        </span>
      )}
    </>
  );
}
