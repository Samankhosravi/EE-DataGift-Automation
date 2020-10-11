﻿using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EEDataGift
{
    public class EESelenium
    {
        private readonly string _username;
        private readonly string _password;

        public EESelenium(string username, string password)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
        }

        internal async Task DataGift(
            string donorTelephone,
            string recipientTelephone,
            int mbToGift)
        {
            var from = (donorTelephone ?? throw new ArgumentNullException(nameof(donorTelephone))).TrimStart('0');
            var to = (recipientTelephone ?? throw new ArgumentNullException(nameof(recipientTelephone)))?.TrimStart('0');

            IWebDriver driver = new ChromeDriver();
            
            try
            {
                driver.Url = "https://id.ee.co.uk/id/login";

                await Task.Delay(5000);
                
                // Cookie warning popup
                foreach (var btn in driver.FindElements(By.TagName("button")))
                {
                    if (btn.Text.Contains("accept", StringComparison.OrdinalIgnoreCase))
                        btn.Click();
                }
                await Task.Delay(50);

                // complete login
                driver.FindElement(By.Name("username")).SendKeys(_username);
                await Task.Delay(500);
                driver.FindElement(By.Name("password")).SendKeys(_password);
                await Task.Delay(500);

                driver.FindElement(By.Name("submitButton")).Click();
                await Task.Delay(3000);

                // navigate to family gifting page.
                driver.Navigate().GoToUrl("https://myaccount.ee.co.uk/app/family-gifting");
                await Task.Delay(3000);

                SelectElement selectDonor = null;
                SelectElement selectRecipient = null;

                // find both drop downs for donor and recepient phone numbers.
                var selects = driver.FindElements(By.TagName("select"));
                foreach (var sel in selects)
                {
                    if (sel.GetAttribute("form")?.Contains("giftDataForm", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (sel.GetAttribute("id")?.Contains("donor", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            selectDonor = new SelectElement(sel);
                        }
                        else if (sel.GetAttribute("id")?.Contains("recipient", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            selectRecipient = new SelectElement(sel);
                        }
                    }
                }

                if (selectDonor == null || selectRecipient == null)
                    throw new Exception("Couldn't find donor and recipient <select> dropdown elements");

                // These take a while to populate so keep trying...
                const int maxAttempts = 10;
                var attempts = 0;
                while (true)
                {
                    try
                    {
                        // Select the donor in the donor drop down
                        selectDonor.SelectByText(from, true);
                        await Task.Delay(50);
                        // Select the recepient in the recepient drop down
                        selectRecipient.SelectByText(to, true);
                        await Task.Delay(75);
                        break;
                    }
                    catch
                    {
                        if (attempts++ > maxAttempts)
                            throw;
                        else
                            await Task.Delay(1000);
                    }
                }

                // locate the entire giftDataForm
                var form = driver
                    .FindElement(By.Id("giftDataForm"));

                // Search for the amount (MB/GB) display span
                var mbLabel = form.FindElements(By.TagName("span"))
                    .Single(s => s.GetAttribute("class")?.Contains("giftingDisplayAmount loaded", StringComparison.OrdinalIgnoreCase) == true);

                // Search for the increment button
                var incrementButton = form.FindElements(By.TagName("button"))
                    .Single(s => s.GetAttribute("class")?.Contains("ee-icon-plus", StringComparison.OrdinalIgnoreCase) == true);

                // While the display shows less than we want to gift, click the increment button
                var currentGift = ParseMegabytes(mbLabel.Text);
                while (currentGift < mbToGift)
                {
                    incrementButton.Click();
                    await Task.Delay(50);

                    currentGift = ParseMegabytes(mbLabel.Text);
                }

                // Now find and click the "Gift" button
                var giftButton = form.FindElements(By.TagName("button"))
                    .Single(s => s.GetAttribute("class")?.Contains("gift", StringComparison.OrdinalIgnoreCase) == true
                        && s.Text?.Contains("gift", StringComparison.OrdinalIgnoreCase) == true);

                Console.WriteLine($"Gifting {currentGift}MB from 0{from} to 0{to}.");

                giftButton.Click();

                await Task.Delay(50);

                // Now find and click the "Yes" confirmation button
                var confirmButton = form.FindElements(By.TagName("button"))
                    .Single(s => s.GetAttribute("type")?.Contains("submit", StringComparison.OrdinalIgnoreCase) == true
                        && s.Text?.Contains("yes", StringComparison.OrdinalIgnoreCase) == true);

                confirmButton.Click();

                Console.WriteLine($"Gifted {currentGift}MB from 0{from} to 0{to}.");

                await Task.Delay(2000);
            }
            finally
            {
                await Task.Delay(500);
                driver.Quit();
            }

            decimal ParseMegabytes(string text)
            {
                if (text.Contains("MB"))
                {
                    return decimal.Parse(text.Replace("MB", "").Trim());
                }
                else if (text.Contains("GB"))
                {
                    return decimal.Parse(text.Replace("GB", "").Trim()) * 1000;
                }

                throw new InvalidOperationException($"Cannot parse text {text}");
            }
        }
    }
}
