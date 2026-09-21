/**
 * Agency dashboard — 7-day sales chart (ApexCharts)
 * Runs after DOM ready — Vendor/Page scripts load before @RenderBody.
 */
'use strict';

(function () {
  async function fetchChartData() {
    try {
      var response = await fetch('/Agency/GetSalesChartValues');
      if (!response.ok) return null;
      return await response.json();
    } catch (err) {
      console.error('[agency-dashboard] chart fetch failed', err);
      return null;
    }
  }

  async function initChart() {
    var el = document.querySelector('#agencyWeeklyChart');
    if (!el) return;

    if (typeof ApexCharts === 'undefined') {
      console.warn('[agency-dashboard] ApexCharts is not loaded');
      return;
    }

    if (el.dataset.chartReady === '1') return;
    el.dataset.chartReady = '1';

    var data = await fetchChartData();
    if (!data || typeof data !== 'object') {
      el.innerHTML = '<p class="text-muted text-center mb-0 py-5">داده‌ای برای نمایش نمودار نیست</p>';
      return;
    }

    var categories = Object.keys(data);
    var values = Object.values(data).map(function (v) {
      var n = Number(v);
      return isNaN(n) ? 0 : n;
    });

    if (!categories.length) {
      el.innerHTML = '<p class="text-muted text-center mb-0 py-5">داده‌ای برای نمایش نمودار نیست</p>';
      return;
    }

    var maxVal = Math.max.apply(null, values.concat([0]));
    // 50% less headroom than Apex default (~2×) → ~1.25× max
    var yMax = maxVal <= 0 ? 1 : Math.ceil(maxVal * 1.25);

    var chart = new ApexCharts(el, {
      chart: {
        height: 340,
        parentHeightOffset: 0,
        type: 'bar',
        toolbar: { show: false },
        fontFamily: 'inherit',
        animations: { enabled: true, speed: 450 }
      },
      plotOptions: {
        bar: {
          columnWidth: '48%',
          borderRadius: 5,
          distributed: false
        }
      },
      grid: {
        show: false,
        padding: { top: 12, bottom: 0, left: -4, right: -4 }
      },
      colors: ['#18181b'],
      dataLabels: {
        enabled: true,
        offsetY: -2,
        formatter: function (val) {
          var n = Math.round(val);
          return n <= 0 ? '۰' : n.toLocaleString('fa-IR');
        },
        style: {
          fontSize: '12px',
          fontWeight: 700,
          colors: ['#ffffff']
        }
      },
      series: [
        {
          name: 'فروش',
          data: values
        }
      ],
      legend: { show: false },
      xaxis: {
        categories: categories,
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: {
          style: { colors: '#52525b', fontSize: '12px' }
        }
      },
      yaxis: {
        min: 0,
        max: yMax,
        forceNiceScale: false,
        tickAmount: 4,
        labels: { show: false }
      },
      tooltip: {
        enabled: true,
        y: {
          formatter: function (val) {
            return Math.round(val).toLocaleString('fa-IR') + ' بلیط';
          }
        }
      }
    });

    chart.render();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initChart);
  } else {
    initChart();
  }
})();
