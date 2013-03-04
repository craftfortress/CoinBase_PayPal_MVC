using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Web.Mvc.Ajax;
using System.Text;
using System.IO;
using System.Web.Security;
using Postal;
using Namespace.Models;
using System.Web.Script.Serialization;

using System.Net.Mail;


namespace Namespace.Controllers
{


    public class PayController : Controller
    {
        /// <summary>
        /// 
        /// </summary>
        ///ICMSRepository _cmsRepository;
        ///     ICustomerRepository _customerRepository;
        ///     IObjectStore _objectStore;

        // public PayPalController()
        //ICustomerRepository customerRepository,
        //      ICMSRepository cmsRepository,
        //      IObjectStore objectStore) : base(customerRepository,objectStore,cmsRepository) {
        //      _cmsRepository = cmsRepository;
        //      _objectStore = objectStore;
        //      _customerRepository = customerRepository;
        //      this.ThemeName = "Admin";

        public string txtEmail = "";
        public string UserName = "";
        public string LastName = "";
        public string PayPalPDTToken = "j7tW2fkAZstG0qtd5wflg-Y9AomS3vJ8b-iofSj6co-DG14OwqLIxCJToeS";
        public string orderID = "";
        public logger _logger = new logger();
         
        Repo rp = new Repo();


        public ActionResult IPN()
        {

            booking bk = new booking();
            var formVals = new Dictionary<string, string>();
            formVals.Add("cmd", "_notify-validate");
            string response = GetPayPalResponse(formVals, false);
             
            string transactionID = Request["txn_id"];
            string sAmountPaid = Request["mc_gross"];
            int tranID = Convert.ToInt32(Request["custom"].ToString());
            bk = rp.GetBooking(tranID);

            _logger.Info("IPN Verified for order" + orderID);

            ////add.UserName = order.UserName;
            ////  _pipeline.AcceptPalPayment(orderID, transactionID, amountPaid);

            _logger.Info("IPN Order successfully transacted: " + orderID);
            _logger.Info(txtEmail);
            ViewBag.Message = response;
            bk.processbooking();
            rp.Save();

            Sendemail("A new booking has been created and paid for.\nRegards Namespace", "bookings@Namespace.com", "Booking " + bk.id + " for " + bk.clientemail + " to be sent to " + bk.targetemail + " accepted by PayPal.");

            ViewBag.Message += "OrderID" + bk.ToString();

            return View();
             
        }


        public ActionResult Sendemail(string message, string to, string sub)
        {
 
            try
            {

                MailMessage cm = new MailMessage("bookings@Namespace.com", to, sub, message);

                SmtpClient sm = new SmtpClient();
                sm.Send(cm);

            }
            catch (Exception rar)
            {
            }


            return null;
        }


        public ActionResult BitcoinIPN()
        {
            return View();
        }

        [HttpPost]
        public void BitcoinIPN(CoinBase order)
        {

            var jsonObject = order;

            booking bk = new booking();
            Repo rp = new Repo();


            try
            {
                var booking = rp.GetBooking(Convert.ToInt32(order.custom));
                booking.state = "Paid";
                booking.notetype = "BitCoin";
                booking.cost = Convert.ToDouble(jsonObject.total_btc.cents);

                rp.Save();

                MembershipUser user = Membership.GetUser(User.Identity.Name);

            }
            catch (Exception e)
            {
                Sendemail("BitCoin Booking Problem", "bookings@Namespace.com", "Error");
            }

            Sendemail("Bitcoin Payment OrderID:" + jsonObject.custom + " Bitcoin Amount " + jsonObject.total_btc.cents + " Booking " + bk.id + " for " + bk.clientemail + " to be sent to " + bk.targetemail, "bookings@Namespace.com", "Bitcoin Payment Received");

        }



        public ActionResult PDT()
        {
            string transactionID = Request.QueryString["tx"];
            string sAmountPaid = Request.QueryString["amt"];
            string orderID = Request.QueryString["cm"];
            string success = Request.QueryString["st"];

            booking bk = new booking();
            Repo rp = new Repo();


            Dictionary<string, string> formVals = new Dictionary<string, string>();
            try
            {
                formVals.Add("cmd", "_notify-synch");
                formVals.Add("at", PayPalPDTToken);
                formVals.Add("tx", transactionID);
                int tranID = Convert.ToInt32(orderID);
                bk = rp.GetBooking(tranID);
            }
            catch
            {
                ViewBag.Message += "it done goofed";

            }

            string response = GetPayPalResponse(formVals, false);
            _logger.Info("PDT Response received for order " + orderID);

            Decimal amountPaid = 0;
            Decimal.TryParse(sAmountPaid, out amountPaid);
            UserName = GetPDTValue(response, "first_name");
            LastName = GetPDTValue(response, "last_name");
            txtEmail = GetPDTValue(response, "payer_email");

            if (response.Contains("SUCCESS"))
            {
                bk.notetype = "PayPal";
                bk.processbooking();
                rp.Save();

                Sendemail("A new booking has been created and paid for.\nRegards Namespace", "bookings@Namespace.com", "Booking " + bk.id + " for " + bk.clientemail + " to be sent to " + bk.targetemail + " accepted by PayPal.");
                return RedirectToAction("../note/mynotes");

            }
            else
            {
                Sendemail("Booking Error.  A new booking has been created and paid for.\nRegards Namespace", "bookings@Namespace.com", "Booking " + bk.id + " for " + bk.clientemail + " to be sent to " + bk.targetemail + " accepted by PayPal.");
                ViewBag.Message += "it done goofed. We have been notified and will do our best to verify you have paid, and complete the booking.";
                return View();
            }

        }


        public string GetPayPalResponse(Dictionary<string, string> formVals, bool useSandbox)
        {

            string paypalUrl = useSandbox ? "https://www.sandbox.paypal.com/cgi-bin/webscr" : "https://www.paypal.com/cgi-bin/webscr";

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(paypalUrl);

            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            byte[] param = Request.BinaryRead(Request.ContentLength);
            string strRequest = Encoding.ASCII.GetString(param);

            StringBuilder sb = new StringBuilder();
            sb.Append(strRequest);

            foreach (string key in formVals.Keys)
            {
                sb.AppendFormat("&{0}={1}", key, formVals[key]);
            }
            strRequest += sb.ToString();
            req.ContentLength = strRequest.Length;

            //WebProxy proxy = new WebProxy(new Uri("http://urlort#");
            //req.Proxy = proxy;

            string response = "";
            using (StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII))
            {

                streamOut.Write(strRequest);
                streamOut.Close();
                using (StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    response = streamIn.ReadToEnd();
                }
            }

            return response;
        }


        public string GetPDTValue(string pdt, string key)
        {
            string[] keys = pdt.Split('\n');
            string thisVal = "";
            string thisKey = "";
            foreach (string s in keys)
            {
                string[] bits = s.Split('=');
                if (bits.Length > 1)
                {
                    thisVal = bits[1];
                    thisKey = bits[0];
                    if (thisKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                        break;
                }
            }
            return thisVal;

        }

    }

}