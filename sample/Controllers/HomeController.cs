using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BackgroundWork;
using Microsoft.AspNetCore.Mvc;

namespace sample.Controllers
{
    public class HomeController : Controller
    {
        IBackgroundWorkScheduler _backgroundWorker;

        public HomeController(IBackgroundWorkScheduler backgroundWorker)
        {
            _backgroundWorker = backgroundWorker;
        }

        public IActionResult Index()
        {

            return View();
        }

        public IActionResult About()
        {

            var sw = Stopwatch.StartNew();
            var queueTime = DateTime.UtcNow;

            for (int i = 0; i < 1000; i++)
            {
                _backgroundWorker.QueueWork(async (ct) => {
                    //Wait a random amount of time, this gives a spread of completion times
                    //so that you can see more movement in the output. Also gives time to kill
                    //the app and see them stop.
                    await Task.Delay(new Random().Next(10000), ct);
                    if(ct.IsCancellationRequested) { return; }
                    Console.WriteLine($"QueueTime: {queueTime.ToLongTimeString()}. CurrentTime: {DateTime.UtcNow.ToLongTimeString()}");
                });
            }

            ViewData["Message"] = $"Queued 1000 items in {sw.ElapsedMilliseconds} milliseconds.";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
