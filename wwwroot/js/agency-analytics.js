/**
 * Agency analytics dashboard chart (ApexCharts)
 * Runs after DOM ready — Vendor/Page scripts load before @RenderBody in the master layout.
 */
'use strict';

(function () {
  function readJson(root, attr) {
    try {
      return JSON.parse(root.getAttribute(attr) || '[]');
    } catch (e) {
      return [];
    }
  }

  function initChart() {
    var root = document.querySelector('.agency-analytics');
    var el = document.querySelector('#agencyAnalyticsChart');
    if (!root || !el) return;

    if (typeof ApexCharts === 'undefined') {
      console.warn('[agency-analytics] ApexCharts is not loaded');
      return;
    }

    if (el.dataset.chartReady === '1') return;
    el.dataset.chartReady = '1';

    var labels = readJson(root, 'data-chart-labels');
    var trips = readJson(root, 'data-chart-trips');
    var spend = readJson(root, 'data-chart-spend');

    if (!labels.length) {
      el.innerHTML = '<p class="text-muted text-center mb-0 py-5">داده‌ای برای نمایش نمودار نیست</p>';
      return;
    }

    var chart = new ApexCharts(el, {
      chart: {
        type: 'line',
        height: window.matchMedia('(max-width: 767.98px)').matches ? 220 : 300,
        parentHeightOffset: 0,
        toolbar: { show: false },
        fontFamily: 'inherit',
        animations: { enabled: true }
      },
      series: [
        { name: 'سفر', type: 'column', data: trips },
        { name: 'هزینه (تومان)', type: 'area', data: spend }
      ],
      stroke: { width: [0, 2], curve: 'smooth' },
      fill: { opacity: [1, 0.18] },
      plotOptions: {
        bar: { columnWidth: '42%', borderRadius: 4 }
      },
      colors: ['#000000', '#71717a'],
      dataLabels: { enabled: false },
      legend: {
        show: true,
        position: 'top',
        horizontalAlign: 'right'
      },
      grid: {
        borderColor: 'rgba(0,0,0,0.06)',
        strokeDashArray: 4,
        padding: { top: -10, bottom: -8 }
      },
      xaxis: {
        categories: labels,
        labels: { rotate: -35, style: { fontSize: '11px' } },
        axisBorder: { show: false },
        axisTicks: { show: false }
      },
      yaxis: [
        {
          title: { text: 'سفر' },
          labels: {
            formatter: function (v) {
              return Math.round(v).toLocaleString('fa-IR');
            }
          }
        },
        {
          opposite: true,
          title: { text: 'تومان' },
          labels: {
            formatter: function (v) {
              return Math.round(v).toLocaleString('fa-IR');
            }
          }
        }
      ],
      tooltip: {
        shared: true,
        y: {
          formatter: function (v) {
            return Math.round(v).toLocaleString('fa-IR');
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
