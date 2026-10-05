using OF.Data.Database;

namespace OF.UI.ViewModels.ChangeOrders
{
    public class ChangeOrdersViewModel
    {
        public Header Header { get; set; }

        public List<ChangeOrderLineWithChange> Lines { get; set; }

        public CpqService[] Services { get; set; }

        public ChangeOrder? ChangeOrder { get; set; }

        public List<ChangeOrderCommentModel> ChangeOrderComments
        {
            get
            {
                if (_changeOrderComments != null)
                {
                    return _changeOrderComments;
                }

                _changeOrderComments = new List<ChangeOrderCommentModel>();

                if (ChangeOrder != null)
                {
                    _changeOrderComments.Add(new ChangeOrderCommentModel()
                    {
                        Comment = $"Change created!",
                        CreatedBy = ChangeOrder.CreatedBy,
                        CreatedOn = ChangeOrder.CreatedDate,
                        Status = ChangeStatus.Requested
                    });

                    if (!string.IsNullOrWhiteSpace(ChangeOrder?.ApprovedBy))
                    {
                        _changeOrderComments.Add(new ChangeOrderCommentModel()
                        {
                            Comment = $"Change Approved!",
                            CreatedBy = ChangeOrder.ApprovedBy,
                            CreatedOn = ChangeOrder.ApprovedDate!.Value,
                            Status = ChangeStatus.Approved
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(ChangeOrder?.RejectedBy))
                    {
                        _changeOrderComments.Add(new ChangeOrderCommentModel()
                        {
                            Comment = $"Change Rejected!",
                            CreatedBy = ChangeOrder.RejectedBy,
                            CreatedOn = ChangeOrder.RejectedDate!.Value,
                            Status = ChangeStatus.Rejected
                        });
                    }

                    if (ChangeOrder?.CompletedDate.HasValue == true)
                    {
                        _changeOrderComments.Add(new ChangeOrderCommentModel()
                        {
                            Comment = $"Change Comepleted!",
                            CreatedBy = "System",
                            CreatedOn = ChangeOrder.CompletedDate!.Value,
                            Status = ChangeStatus.Completed
                        });
                    }

                    if (ChangeOrder?.ChangeOrderComments?.Any() == true)
                    {
                        _changeOrderComments.AddRange(ChangeOrder?.ChangeOrderComments.Select(i => new ChangeOrderCommentModel()
                        {
                            Comment = i.Comment,
                            CreatedBy = i.CreatedBy,
                            CreatedOn = i.CreatedDate
                        }));

                        _changeOrderComments = _changeOrderComments.OrderByDescending(i => i.CreatedOn).ToList();
                    }
                }


                return _changeOrderComments;
            }
        }

        private List<ChangeOrderCommentModel> _changeOrderComments { get; set; }
    }

    public class ChangeOrderLineWithChange
    {
        public int Index { get; set; }

        public Line? Line { get; set; }

        public ChangeOrderLine? ChangeOrderLine { get; set; }
    }

    public class ChangeOrderCommentModel
    {
        public DateTime CreatedOn { get; set; }

        public string CreatedBy { get; set; }

        public string Comment { get; set; }

        public ChangeStatus? Status { get; set; }
    }
}
