/**
 * Agency analytics dashboard chart (ApexCharts)
 * Runs after DOM ready — Vendor/Page scripts load before @RenderBody in the master layout.
 * Wide chart scrolls horizontally; Y-axis numbers live in fixed side rails (no scroll lag).
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

  function clamp(n, min, max) {
    return Math.min(max, Math.max(min, n));
  }

  function formatFa(v) {
    return Math.round(v).toLocaleString('fa-IR');
  }

  function bindToolbar(chart, labels) {
    var tools = document.querySelector('.agency-analytics__chart-tools');
    if (!tools || tools.dataset.bound === '1') return;
    tools.dataset.bound = '1';

    var lastIndex = Math.max(0, labels.length - 1);

    function currentRange() {
      var min = chart.w.globals.minX;
      var max = chart.w.globals.maxX;
      if (typeof min !== 'number' || typeof max !== 'number' || isNaN(min) || isNaN(max)) {
        return { min: 0, max: lastIndex };
      }
      return { min: min, max: max };
    }

    tools.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-chart-action]');
      if (!btn) return;

      var action = btn.getAttribute('data-chart-action');
      var range = currentRange();
      var span = Math.max(1, range.max - range.min);

      if (action === 'zoomin') {
        if (span <= 2) return;
        var shrink = span * 0.22;
        chart.zoomX(range.min + shrink, range.max - shrink);
        return;
      }

      if (action === 'zoomout') {
        var grow = Math.max(1, span * 0.35);
        chart.zoomX(clamp(range.min - grow, 0, lastIndex), clamp(range.max + grow, 0, lastIndex));
        return;
      }

      if (action === 'reset') {
        chart.zoomX(0, lastIndex);
      }
    });
  }

  function measureHostWidth(scrollWrap, el) {
    if (scrollWrap) {
      var w = scrollWrap.clientWidth;
      if (w > 0) return w;
    }
    var parent = el.parentElement;
    return (parent && parent.clientWidth) || window.innerWidth || 640;
  }

  function resolveChartWidth(pointCount, hostWidth, isMobile) {
    var pxPerPoint = isMobile ? 64 : 72;
    var contentWidth = Math.max(pointCount * pxPerPoint, pointCount > 10 ? hostWidth + 280 : hostWidth);
    return Math.max(hostWidth, contentWidth);
  }

  function lockChartShellWidth(el, width) {
    el.style.width = width + 'px';
    el.style.minWidth = width + 'px';
    el.style.maxWidth = width + 'px';
  }

  function readAxisTicks(chart, axisIndex) {
    var scales = chart.w.globals.yAxisScale || [];
    var scale = scales[axisIndex];
    if (scale && Array.isArray(scale.result) && scale.result.length) {
      return scale.result.slice();
    }

    var nice = chart.w.globals.yRange && chart.w.globals.yRange[axisIndex];
    if (nice && typeof nice.min !== 'undefined') {
      return [nice.min, nice.max];
    }

    return [0, 1];
  }

  function paintAxisRail(rail, ticks, topPad, gridHeight) {
    if (!rail) return;

    var ordered = ticks.slice();
    // Apex draws max at the top — reverse ascending scales so max is first in the flex column
    if (ordered.length >= 2 && ordered[0] < ordered[ordered.length - 1]) {
      ordered.reverse();
    }

    rail.style.paddingTop = Math.max(0, topPad) + 'px';
    rail.style.height = Math.max(0, topPad) + Math.max(0, gridHeight) + 'px';
    rail.innerHTML = '';

    var list = document.createElement('div');
    list.className = 'agency-analytics__yaxis-rail-ticks';
    list.style.height = Math.max(0, gridHeight) + 'px';

    ordered.forEach(function (tick) {
      var span = document.createElement('span');
      span.className = 'agency-analytics__yaxis-rail-tick';
      span.textContent = formatFa(tick);
      list.appendChild(span);
    });

    rail.appendChild(list);
  }

  function syncAxisRails(chart) {
    var frame = document.querySelector('.agency-analytics__chart-frame');
    if (!frame || !chart || !chart.w || !chart.w.globals) return;

    var g = chart.w.globals;
    var topPad = g.translateY || 0;
    var gridHeight = g.gridHeight || 0;

    paintAxisRail(
      frame.querySelector('[data-yaxis-rail="0"]'),
      readAxisTicks(chart, 0),
      topPad,
      gridHeight
    );
    paintAxisRail(
      frame.querySelector('[data-yaxis-rail="1"]'),
      readAxisTicks(chart, 1),
      topPad,
      gridHeight
    );
  }

  function bindAxisRails(chart) {
    var sync = function () {
      requestAnimationFrame(function () {
        syncAxisRails(chart);
      });
    };

    if (typeof chart.addEventListener === 'function') {
      chart.addEventListener('zoomed', sync);
      chart.addEventListener('updated', sync);
      chart.addEventListener('animationEnd', sync);
    }

    sync();
  }

  function initChart() {
    var root = document.querySelector('.agency-analytics');
    var el = document.querySelector('#agencyAnalyticsChart');
    var scrollWrap = document.querySelector('.agency-analytics__chart-scroll');
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

    var isMobile = window.matchMedia('(max-width: 767.98px)').matches;
    // Rails sit outside the scrollport — measure after layout; start from scroll parent width
    var hostWidth = measureHostWidth(scrollWrap, el);
    var chartWidth = resolveChartWidth(labels.length, hostWidth, isMobile);

    lockChartShellWidth(el, chartWidth);

    var chart = new ApexCharts(el, {
      chart: {
        type: 'line',
        height: isMobile ? 240 : 320,
        width: chartWidth,
        parentHeightOffset: 0,
        fontFamily: 'inherit',
        animations: { enabled: true },
        redrawOnWindowResize: false,
        zoom: {
          enabled: true,
          type: 'x',
          autoScaleYaxis: true
        },
        selection: {
          enabled: true,
          type: 'x'
        },
        toolbar: { show: false }
      },
      series: [
        { name: 'سفر', type: 'column', data: trips },
        { name: 'هزینه (تومان)', type: 'area', data: spend }
      ],
      stroke: { width: [0, 2], curve: 'smooth' },
      fill: { opacity: [1, 0.18] },
      plotOptions: {
        bar: { columnWidth: labels.length > 20 ? '55%' : '42%', borderRadius: 4 }
      },
      colors: ['#000000', '#71717a'],
      dataLabels: { enabled: false },
      legend: { show: false },
      grid: {
        borderColor: 'rgba(0,0,0,0.06)',
        strokeDashArray: 4,
        padding: { top: 8, bottom: 4, left: 4, right: 4 }
      },
      xaxis: {
        categories: labels,
        tickPlacement: 'on',
        labels: {
          rotate: -40,
          rotateAlways: labels.length > 12,
          hideOverlappingLabels: false,
          trim: false,
          style: { fontSize: '11px' }
        },
        axisBorder: { show: false },
        axisTicks: { show: true }
      },
      yaxis: [
        {
          labels: { show: false },
          axisBorder: { show: false },
          axisTicks: { show: false }
        },
        {
          opposite: true,
          labels: { show: false },
          axisBorder: { show: false },
          axisTicks: { show: false }
        }
      ],
      tooltip: {
        shared: true,
        y: {
          formatter: function (v) {
            return formatFa(v);
          }
        }
      }
    });

    chart.render().then(function () {
      // Re-measure scroll width now that rails took some space
      var nextHost = measureHostWidth(scrollWrap, el);
      var nextWidth = resolveChartWidth(labels.length, nextHost, isMobile);
      if (nextWidth !== chartWidth) {
        chartWidth = nextWidth;
        lockChartShellWidth(el, chartWidth);
        chart.updateOptions({ chart: { width: chartWidth } }, false, false);
      }

      var canvas = el.querySelector('.apexcharts-canvas');
      if (canvas) {
        canvas.style.width = chartWidth + 'px';
        canvas.style.minWidth = chartWidth + 'px';
      }

      bindToolbar(chart, labels);
      bindAxisRails(chart);

      if (scrollWrap && chartWidth > nextHost + 8) {
        requestAnimationFrame(function () {
          var isRtl =
            document.documentElement.getAttribute('dir') === 'rtl' ||
            document.body.getAttribute('dir') === 'rtl';
          if (isRtl) {
            scrollWrap.scrollLeft = 0;
          } else {
            scrollWrap.scrollLeft = Math.max(0, chartWidth - nextHost);
          }
        });
      }
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initChart);
  } else {
    initChart();
  }
})();
