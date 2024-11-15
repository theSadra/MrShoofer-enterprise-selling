/**
 * Page Detail overview
 */

'use strict';

// Datatable (jquery)
$(function () {

  var agency_id = $("#agencyid").val();
  // Variable declaration for table
  var dt_customer_order = $('.datatables-customer-order'),
    order_details = '/Ecommerce/OrderDetails',
    statusObj = {
      1: { title: 'آماده بارگیری', class: 'bg-label-info' },
      2: { title: 'ناموفق', class: 'bg-label-warning' },
      3: { title: 'تحویل داده', class: 'bg-label-success' },
      4: { title: 'تحویل دیرهنگام', class: 'bg-label-primary' }
    };

  // orders datatable
  if (dt_customer_order.length) {
    var dt_order = dt_customer_order.DataTable({
      ajax: "/Admin/Agency/GetAgencyTickets?id="+ agency_id, // JSON file to add data
      columns: [
        // columns according to JSON
        { data: '' },
        { data: 'id' },
        { data: 'origin' },
        { data: 'firstname' },
        { data: 'code' },
        { data: 'price' },
        { data: ' ' }
      ],
      columnDefs: [
        {
          // For Responsive
          className: 'control',
          searchable: false,
          orderable: false,
          responsivePriority: 2,
          targets: 0,
          render: function (data, type, full, meta) {
            return '';
          }
        },
        {
          // For Checkboxes
          targets: 1,
          responsivePriority: 3,
          render: function (data, type, full, meta) {
            var $code = full["code"];
            return '<a> '+ $code + "</a>";
          }
        },
        {
          // order order number
          targets: 2,
          responsivePriority: 4,
          render: function (data, type, full, meta) {
            var $origin = full['origin'];
            var $dest = full['dest'];


            var $servicename = full['servicename'];
            var $carname = full['carname'];

            return (
              "<div>"
              + "<small class='d-block badge bg-label-dark rounded-pill py-0'>"
              + $servicename
              + "</small>"

              + "<p class='fw-medium mb-0 text-center'><span>" + $origin + "<span class='text-muted'> به </span> " + $dest + '</span></p>'
              
              + "<small class='d-block text-center badge bg-label-warning rounded-pill py-0 text-black'>"
              + $carname
              + "</small>"

              + "</div>"
            );
          }
        },
        {
          // date
          targets: 3,
          render: function (data, type, full, meta) {
            var $firstname = full["firstname"];
            var $lastname = full["lastname"];
            var $phone = full["phonenumber"];
            
            return '<div><label>' + $firstname +' ' + $lastname + '</label><a class="d-block" href="tel:'+$phone+ '">' +$phone + '</a></div>';
          }
        },
        {
          // status
          targets: 4,
          render: function (data, type, full, meta) {
            var $date = full['registeredAt_date'];
            var $time = full['registeredAt_time'];

            return '<div><label>' + $date + '</label><lable class="d-block text-muted">' + $time  + '</lable></div>';
          }
        },
        {
          // spent
          targets: 5,
          render: function (data, type, full, meta) {
            var $price = full['price'];

            return '<span>' + $price + '</span>';
          }
        },
        {
          // Actions
          targets: -1,
          title: 'عملیات',
          searchable: false,
          orderable: false,
          render: function (data, type, full, meta) {
            return (
              '<div class="text-xxl-center">' +
              '<button class="btn btn-sm btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="ti ti-dots-vertical"></i></button>' +
              '<div class="dropdown-menu dropdown-menu-end m-0">' +
              '<a href="javascript:;" class="dropdown-item">مشاهده</a>' +
              '<a href="javascript:;" class="dropdown-item  delete-record">حذف</a>' +
              '</div>' +
              '</div>'
            );
          }
        }
      ],
/*      order: [[2, 'desc']],*/
      dom:
        '<"card-header flex-column flex-md-row py-2"<"head-label text-center pt-2 pt-md-0">f' +
        '>t' +
        '<"row mx-4"' +
        '<"col-md-12 col-xl-6 text-center text-xl-start pb-2 pb-lg-0 pe-0"i>' +
        '<"col-md-12 col-xl-6 d-flex justify-content-center justify-content-xl-end"p>' +
        '>',
      lengthMenu: [6, 30, 50, 70, 100],
      language: {
        sLengthMenu: '_MENU_',
        search: '',
        searchPlaceholder: 'جستجو سفر'
      },
      // Buttons with Dropdown

      // For responsive popup
      responsive: {
        details: {
          display: $.fn.dataTable.Responsive.display.modal({
            header: function (row) {
              var data = row.data();
              return 'جزئیات ' + data['order'];
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
    $('div.head-label').html('<h5 class="card-title mb-0 text-nowrap text-start"><i class="ti ti-ticket ti-md"></i> سفرهای ثبت‌شده</h5>');
  }

  // Delete Record
  $('.datatables-orders tbody').on('click', '.delete-record', function () {
    dt_order.row($(this).parents('tr')).remove().draw();
  });

  // Filter form control to default size
  // ? setTimeout used for multilingual table initialization
  setTimeout(() => {
    $('.dataTables_filter .form-control').removeClass('form-control-sm');
    $('.dataTables_length .form-select').removeClass('form-select-sm');
  }, 300);
});
