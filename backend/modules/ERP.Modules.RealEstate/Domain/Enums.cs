namespace ERP.Modules.RealEstate.Domain;

public enum UnitUsage { Residential = 1, Commercial = 2 }
public enum UnitStatus { Vacant = 1, Reserved = 2, Occupied = 3, Unavailable = 4 }
public enum PropertyType { Residential = 1, Villa = 2, Commercial = 3, MixedUse = 4, Retail = 5, Industrial = 6, Land = 7 }
public enum OwnershipType { Owned = 1, ManagedForOwner = 2 }
public enum LeaseStatus { Draft = 0, Active = 1, Ended = 2, Renewed = 3, Terminated = 4, HoldingOver = 5, Cancelled = 6 }
public enum InstalmentStatus { Scheduled = 0, Invoiced = 1, Cancelled = 2 }
public enum CostResponsibility { Owner = 1, Company = 2, Tenant = 3, Shared = 4 }
public enum Priority { Low = 1, Normal = 2, High = 3, Critical = 4 }
public enum WorkOrderStatus { New = 0, Assigned = 1, InProgress = 2, OnHold = 3, Completed = 4, Closed = 5, Cancelled = 6 }
public enum DealType { Sale = 1, Lease = 2 }
public enum DealStage { Lead = 0, Viewing = 1, Negotiation = 2, Agreed = 3, Invoiced = 4, Collected = 5, AgentPaid = 6, Lost = 9 }
