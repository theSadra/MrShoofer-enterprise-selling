/**
 * Dashboard Analytics
 */

'use strict';

(async function () {
  let cardColor, headingColor, labelColor, shadeColor, grayColor;

  // Determine colors based on the style
  if (isDarkStyle) {
    cardColor = config.colors_dark.cardColor;
    labelColor = config.colors_dark.textMuted;
    headingColor = config.colors_dark.headingColor;
    shadeColor = 'dark';
    grayColor = '#5E6692'; // gray color is for stacked bar chart
  } else {
    cardColor = config.colors.cardColor;
    labelColor = config.colors.textMuted;
    headingColor = config.colors.headingColor;
    shadeColor = '';
    grayColor = '#817D8D';
  }

  // Initialize Swiper
  const swiperWithPagination = document.querySelector('#swiper-with-pagination-cards');
  if (swiperWithPagination) {
    new Swiper(swiperWithPagination, {
      loop: true,
      autoplay: {
        delay: 2500,
        disableOnInteraction: false
      },
      pagination: {
        clickable: true,
        el: '.swiper-pagination'
      }
    });
  }

  // Initialize Revenue Generated Area Chart
  const revenueGeneratedEl = document.querySelector('#revenueGenerated');
  const revenueGeneratedConfig = {
    chart: {
      height: 130,
      type: 'area',
      parentHeightOffset: 0,
      toolbar: {
        show: false
      },
      sparkline: {
        enabled: true
      }
    },
    markers: {
      colors: 'transparent',
      strokeColors: 'transparent'
    },
    grid: {
      show: false
    },
    colors: [config.colors.success],
    fill: {
      type: 'gradient',
      gradient: {
        shade: shadeColor,
        shadeIntensity: 0.8,
        opacityFrom: 0.6,
        opacityTo: 0.1
      }
    },
    dataLabels: {
      enabled: false
    },
    stroke: {
      width: 2,
      curve: 'smooth'
    },
    series: [
      {
        data: [300, 350, 330, 380, 340, 400, 380]
      }
    ],
    xaxis: {
      show: true,
      lines: {
        show: false
      },
      labels: {
        show: false
      },
      stroke: {
        width: 0
      },
      axisBorder: {
        show: false
      }
    },
    yaxis: {
      stroke: {
        width: 0
      },
      show: false
    },
    tooltip: {
      enabled: false
    }
  };

  if (revenueGeneratedEl) {
    const revenueGenerated = new ApexCharts(revenueGeneratedEl, revenueGeneratedConfig);
    revenueGenerated.render();
  }

  // Fetch chart data and initialize the Earning Reports Bar Chart
  try {
    const lastweek_sale_dictionary = await FetchChartData();

    console.log(lastweek_sale_dictionary.keys);
    console.log(lastweek_sale_dictionary);


    const weeklyEarningReportsEl = document.querySelector('#weeklyEarningReports');

    const weeklyEarningReportsConfig = {
      chart: {
        height: 202,
        parentHeightOffset: 0,
        type: 'bar',
        toolbar: {
          show: false
        }
      },
      plotOptions: {
        bar: {
          barHeight: '100%',
          columnWidth: '38%',
          startingShape: 'rounded',
          endingShape: 'rounded',
          borderRadius: 4,
          distributed: false
        }
      },
      grid: {
        show: false,
        padding: {
          top: 0,
          bottom: 0,
          left: -10,
          right: -10
        }
      },
      colors: [
        '#f9cb61',
        '#f9cb61',
        '#f9cb61',
        '#f9cb61',
        '#f9cb61',
        '#f9cb61',
        '#f9cb61'
      ],
      dataLabels: {
        enabled: true,
        formatter: function (val) {
          // Display '0' for values less than 0.1
          return val <= 0.2 ? '0' : val;
        }
      },
      series: [
        {
          name: 'فروش',
          data: Object.values(lastweek_sale_dictionary).map(value => value < 0.2 ? 0.2 : value) // Ensure visibility by using 0.1 for low values
        }
      ],
      legend: {
        show: false
      },
      xaxis: {
        categories: Object.keys(lastweek_sale_dictionary),
        axisBorder: {
          show: false
        },
        axisTicks: {
          show: false
        },
        labels: {
          style: {
            colors: '#232323', // Replace with your label color
            fontSize: '13px',
            fontFamily: 'font-primary'
          },
          useHTML: true
        }
      },
      yaxis: {
        labels: {
          show: false,
          formatter: function (value) {
            return Math.round(value);
          }
        }
      },
      tooltip: {
        enabled: true
      },
      responsive: [
        {
          breakpoint: 1025,
          options: {
            chart: {
              height: 199
            }
          }
        }
      ]
    };

    if (weeklyEarningReportsEl) {
      const weeklyEarningReports = new ApexCharts(weeklyEarningReportsEl, weeklyEarningReportsConfig);
      weeklyEarningReports.render();
    }
  } catch (error) {
    console.error('Error fetching data:', error);
  }



  // Initialize Support Tracker Radial Bar Chart
  const supportTrackerEl = document.querySelector('#supportTracker');
  const supportTrackerOptions = {
    series: [85],
    labels: ['تکمیل شده'],
    chart: {
      height: 360,
      type: 'radialBar'
    },
    plotOptions: {
      radialBar: {
        offsetY: 10,
        startAngle: -140,
        endAngle: 130,
        hollow: {
          size: '65%'
        },
        track: {
          background: cardColor,
          strokeWidth: '100%'
        },
        dataLabels: {
          name: {
            offsetY: -20,
            color: labelColor,
            fontSize: '13px',
            fontWeight: '400',
            fontFamily: 'font-primary'
          },
          value: {
            offsetY: 10,
            color: headingColor,
            fontSize: '38px',
            fontWeight: '500',
            fontFamily: 'font-primary'
          }
        }
      }
    },
    colors: [config.colors.primary],
    fill: {
      type: 'gradient',
      gradient: {
        shade: 'dark',
        shadeIntensity: 0.5,
        gradientToColors: [config.colors.primary],
        inverseColors: true,
        opacityFrom: 1,
        opacityTo: 0.6,
        stops: [30, 70, 100]
      }
    },
    stroke: {
      dashArray: 10
    },
    grid: {
      padding: {
        top: -20,
        bottom: 5
      }
    },
    states: {
      hover: {
        filter: {
          type: 'none'
        }
      },
      active: {
        filter: {
          type: 'none'
        }
      }
    },
    responsive: [
      {
        breakpoint: 1025,
        options: {
          chart: {
            height: 330
          }
        }
      },
      {
        breakpoint: 769,
        options: {
          chart: {
            height: 280
          }
        }
      }
    ]
  };

  if (supportTrackerEl) {
    const supportTracker = new ApexCharts(supportTrackerEl, supportTrackerOptions);
    supportTracker.render();
  }

  // Initialize Total Earning Chart - Bar Chart
  const totalEarningChartEl = document.querySelector('#totalEarningChart');
  const totalEarningChartOptions = {
    series: [
      {
        name: 'درآمد',
        data: [15, 10, 20, 8, 12, 18, 12, 5]
      },
      {
        name: 'هزینه‌ها',
        data: [-7, -10, -7, -12, -6, -9, -5, -8]
      }
    ],
    chart: {
      height: 230,
      parentHeightOffset: 0,
      stacked: true,
      type: 'bar',
      toolbar: { show: false }
    },
    tooltip: {
      enabled: false
    },
    legend: {
      show: false
    },
    plotOptions: {
      bar: {
        horizontal: false,
        columnWidth: '18%',
        borderRadius: 5,
        startingShape: 'rounded',
        endingShape: 'rounded'
      }
    },
    colors: [config.colors.primary, grayColor],
    dataLabels: {
      enabled: false
    },
    grid: {
      show: false,
      padding: {
        top: -40,
        bottom: -20,
        left: -10,
        right: -2
      }
    },
    xaxis: {
      labels: {
        show: false
      },
      axisTicks: {
        show: false
      },
      axisBorder: {
        show: false
      }
    },
    yaxis: {
      labels: {
        show: false
      }
    },
    responsive: [
      {
        breakpoint: 1468,
        options: {
          plotOptions: {
            bar: {
              columnWidth: '22%'
            }
          }
        }
      },
      {
        breakpoint: 1197,
        options: {
          chart: {
            height: 228
          },
          plotOptions: {
            bar: {
              borderRadius: 8,
              columnWidth: '26%'
            }
          }
        }
      },
      {
        breakpoint: 783,
        options: {
          chart: {
            height: 232
          },
          plotOptions: {
            bar: {
              borderRadius: 6,
              columnWidth: '28%'
            }
          }
        }
      },
      {
        breakpoint: 589,
        options: {
          plotOptions: {
            bar: {
              columnWidth: '16%'
            }
          }
        }
      },
      {
        breakpoint: 520,
        options: {
          plotOptions: {
            bar: {
              borderRadius: 6,
              columnWidth: '18%'
            }
          }
        }
      },
      {
        breakpoint: 426,
        options: {
          plotOptions: {
            bar: {
              borderRadius: 5,
              columnWidth: '20%'
            }
          }
        }
      },
      {
        breakpoint: 381,
        options: {
          plotOptions: {
            bar: {
              columnWidth: '24%'
            }
          }
        }
      }
    ],
    states: {
      hover: {
        filter: {
          type: 'none'
        }
      },
      active: {
        filter: {
          type: 'none'
        }
      }
    }
  };

  if (totalEarningChartEl) {
    const totalEarningChart = new ApexCharts(totalEarningChartEl, totalEarningChartOptions);
    totalEarningChart.render();
  }

  // Initialize DataTable
  var dt_projects_table = $('.datatables-projects');
  if (dt_projects_table.length) {
    var dt_project = dt_projects_table.DataTable({
      ajax: assetsPath + 'json/user-profile.json',
      columns: [
        { data: '' },
        { data: 'id' },
        { data: 'project_name' },
        { data: 'project_leader' },
        { data: '' },
        { data: 'status' },
        { data: '' }
      ],
      columnDefs: [
        // ... your existing columnDefs code
      ],
      order: [[2, 'desc']],
      dom: '<"card-header pb-0 pt-sm-0"<"head-label text-center"><"d-flex justify-content-center justify-content-md-end"f>>t<"row mx-2"<"col-sm-12 col-md-6"i><"col-sm-12 col-md-6"p>>',
      displayLength: 5,
      lengthMenu: [5, 10, 25, 50, 75, 100],
      responsive: {
        details: {
          display: $.fn.dataTable.Responsive.display.modal({
            header: function (row) {
              var data = row.data();
              return 'جزئیات پروژه "' + data['project_name'] + "\"";
            }
          }),
          type: 'column',
          renderer: function (api, rowIdx, columns) {
            var data = $.map(columns, function (col, i) {
              return col.title !== '' // ? Do not show row in modal popup if title is blank (for check box)
                ? '<tr data-dt-row="' +
                col.rowIndex +
                '" data-dt-column="' +
                col.columnIndex +
                '">' +
                '<td>' +
                col.title +
                ':' +
                '</td> ' +
                '<td>' +
                col.data +
                '</td>' +
                '</tr>'
                : '';
            }).join('');

            return data ? $('<table class="table"/><tbody />').append(data) : false;
          }
        }
      }
    });
    $('div.head-label').html('<h5 class="card-title mb-0">پروژه‌ها</h5>');
  }

  // Filter form control to default size
  setTimeout(() => {
    $('.dataTables_filter .form-control').removeClass('form-control-sm');
    $('.dataTables_length .form-select').removeClass('form-select-sm');
  }, 300);
})();







async function FetchChartData() {
  try {
    const response = await fetch('/Agency/GetSalesChartValues'); // Adjust URL as necessary
    const jsonData = await response.json(); // Parse the JSON response


    return jsonData;

  } catch (error) {
    console.error('Error fetching data:', error);
    return []; // Return an empty array in case of an error
  }
}
