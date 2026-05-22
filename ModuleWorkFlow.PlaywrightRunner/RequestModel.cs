namespace ModuleWorkFlow.PlaywrightRunner
{
    internal sealed class RequestModel
    {
        public string Action { get; set; }

        public RequestParams Params { get; set; }
    }

    internal sealed class RequestParams
    {
        public string LoginUrl { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string BaseUrl { get; set; }

        public string Customer { get; set; }

        public string ItemNo { get; set; }

        public string Quantity { get; set; }

        public string OrderDate { get; set; }

        public string DeliveryDate { get; set; }

        public string Remark { get; set; }

        public string ModuleId { get; set; }

        public string PartNo { get; set; }

        public string PartNoList { get; set; }

        public string MenuId { get; set; }

        public string PageIndex { get; set; }

        public string ListUrl { get; set; }

        public int? FromPosition { get; set; }

        public int? ToPosition { get; set; }

        public string Direction { get; set; }

        public int? Steps { get; set; }

        public string ProcessName { get; set; }
    }

    internal sealed class LoginRequestParams
    {
        public string LoginUrl { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }

    internal sealed class OrderRequestParams
    {
        public string BaseUrl { get; set; }

        public string Customer { get; set; }

        public string ItemNo { get; set; }

        public string Quantity { get; set; }

        public string OrderDate { get; set; }

        public string DeliveryDate { get; set; }

        public string Remark { get; set; }
    }
}
