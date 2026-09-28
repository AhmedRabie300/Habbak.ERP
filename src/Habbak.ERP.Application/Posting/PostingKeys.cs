using System.Security.Cryptography;
using System.Text;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// The screen codes documents post under, and the idempotency keys they post with.
///
/// Keys are derived from what the entry represents rather than generated per call (spec rule 15),
/// so a retried post of the same invoice arrives with the same key and gets back the entry that
/// already exists instead of a second one.
/// </summary>
public static class PostingKeys
{
    public const string PurchaseInvoiceScreen = Screens.PostingScreenCatalog.PurchaseInvoice;

    /// <summary>One entry's own key within a document posting: the document's key and the template's family.</summary>
    public static Guid Derive(Guid requestKey, Guid templateFamilyId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{requestKey:N}:{templateFamilyId:N}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static Guid For(long companyId, string purpose, long documentId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{companyId}:{purpose}:{documentId}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
