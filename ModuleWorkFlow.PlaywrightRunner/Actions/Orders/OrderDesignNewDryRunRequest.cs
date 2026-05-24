namespace ModuleWorkFlow.PlaywrightRunner.Actions.Orders
{
    public sealed class OrderDesignNewDryRunRequest
    {
        public string BaseUrl { get; set; }

        public string MesUserName { get; set; }

        public string MesPassword { get; set; }

        public string Customer { get; set; }

        public string ItemNo { get; set; }

        public string Quantity { get; set; }

        public string OrderDate { get; set; }

        public string DeliveryDate { get; set; }

        public string Remark { get; set; }
    }
}
