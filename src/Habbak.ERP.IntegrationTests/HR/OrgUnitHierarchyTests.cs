using Habbak.ERP.Domain.HR;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, decision 9 — pure logic, no database: proves
/// OrgUnitHierarchy.WouldCreateCycle against the 5 cases the plan calls out explicitly.
/// </summary>
public class OrgUnitHierarchyTests
{
    [Fact]
    public void Self_loop_is_a_cycle()
    {
        // A's own parent proposed as A itself.
        var parents = new Dictionary<long, long?>();
        Assert.True(OrgUnitHierarchy.WouldCreateCycle(candidateId: 1, proposedParentId: 1, parents));
    }

    [Fact]
    public void Indirect_cycle_is_detected()
    {
        // A -> B already exists (B.ParentId = A). Proposing A.ParentId = B would close the loop.
        var parents = new Dictionary<long, long?> { [2] = 1 }; // B(2).Parent = A(1)
        Assert.True(OrgUnitHierarchy.WouldCreateCycle(candidateId: 1, proposedParentId: 2, parents));
    }

    [Fact]
    public void A_valid_depth_2_chain_is_accepted()
    {
        // Root(1) <- Mid(2). Proposing Leaf(3).ParentId = Mid(2) is fine.
        var parents = new Dictionary<long, long?> { [2] = 1 };
        Assert.False(OrgUnitHierarchy.WouldCreateCycle(candidateId: 3, proposedParentId: 2, parents));
    }

    [Fact]
    public void A_valid_depth_3_chain_is_accepted()
    {
        // Root(1) <- Mid(2) <- Mid2(3). Proposing Leaf(4).ParentId = Mid2(3) is fine.
        var parents = new Dictionary<long, long?> { [2] = 1, [3] = 2 };
        Assert.False(OrgUnitHierarchy.WouldCreateCycle(candidateId: 4, proposedParentId: 3, parents));
    }

    [Fact]
    public void A_cycle_three_levels_up_is_detected()
    {
        // Root(1) <- Mid(2) <- Mid2(3). Proposing Root(1).ParentId = Mid2(3) closes a 3-level loop.
        var parents = new Dictionary<long, long?> { [2] = 1, [3] = 2 };
        Assert.True(OrgUnitHierarchy.WouldCreateCycle(candidateId: 1, proposedParentId: 3, parents));
    }

    [Fact]
    public void A_null_proposed_parent_root_is_never_a_cycle()
    {
        var parents = new Dictionary<long, long?> { [2] = 1, [3] = 2 };
        Assert.False(OrgUnitHierarchy.WouldCreateCycle(candidateId: 5, proposedParentId: null, parents));
    }
}
