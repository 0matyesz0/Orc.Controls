﻿namespace Orc.Controls
{
    using System.Collections.Generic;

    public class CalloutManager : ICalloutManager
    {

        public CalloutManager()
        {
            if(Callouts == null)
                Callouts = new List<CalloutViewModel>();
        }

        public IList<CalloutViewModel> Callouts { get; set; }

        public void Register(CalloutViewModel calloutViewModel, Callout callout)
        {
            if (calloutViewModel != null && callout != null)
            {
                Callouts.Add(calloutViewModel);
                callout.DataContext = calloutViewModel;
            }
        }

        public void UnRegister(CalloutViewModel callout)
        {
            Callouts.Remove(callout);
        }

        public void RemoveAllCallouts()
        {
            Callouts.Clear();
        }
    }
}
