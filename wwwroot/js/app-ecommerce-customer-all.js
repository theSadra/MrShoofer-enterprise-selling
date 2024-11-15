/**
 * App eCommerce customer all
 */

'use strict';

// Datatable (jquery)
$(function () {



  let borderColor, bodyBg, headingColor;

  if (isDarkStyle) {
    borderColor = config.colors_dark.borderColor;
    bodyBg = config.colors_dark.bodyBg;
    headingColor = config.colors_dark.headingColor;
  } else {
    borderColor = config.colors.borderColor;
    bodyBg = config.colors.bodyBg;
    headingColor = config.colors.headingColor;
  }

  // Variable declaration for table
  var dt_customer_table = $('.datatables-customers'),
    select2 = $('.select2'),
    customerView = '/Ecommerce/CustomerDetailsOverview';
  if (select2.length) {
    var $this = select2;
    $this.wrap('<div class="position-relative"></div>').select2({
      placeholder: 'ایران',
      dropdownParent: $this.parent()
    });
  }

  // customers datatable

  if (dt_customer_table.length) {
    var dt_customer = dt_customer_table.DataTable({
      ajax: '/Admin/Agency/GetAgenciesJson', // JSON file to add data
      columns: [
        // columns according to JSON
        { data: null, defaultContent: '' },
        { data: 'id' },
        { data: 'name' },
        { data: 'admin_phone' },
        { data: 'allsoled' }
      ],
      columnDefs: [
        {
          // For Responsive
          className: 'control',
          searchable: false,
          orderable: false,
          responsivePriority: 2,
          targets: 0,
          render: function () {
            return '';
          }
        },
        {
          // For Checkboxes
          targets: 1,
          orderable: false,
          searchable: false,
          responsivePriority: 3,
          checkboxes: true,
          render: function () {
            return '<input type="checkbox" class="dt-checkboxes form-check-input">';
          },
          checkboxes: {
            selectAllRender: '<input type="checkbox" class="form-check-input">'
          }
        },
        {
          // customer full name and email
          targets: 2,
          responsivePriority: 1,
          render: function (data, type, full, meta) {
            var $name = full['name'];

              // For Avatar badge
              var stateNum = Math.floor(Math.random() * 6);
              var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
              var $state = states[stateNum];
              $name = full['name'];
              let nameParts = $name.split(" ");

            let $initials = "";
            if (nameParts.length > 1) {

              $initials = nameParts[0].charAt(0) + "‌" + nameParts[1].charAt(0);
            }
            else {
              $initials = nameParts[0].charAt(0);
            }
              var $output = '<span class="avatar-initial rounded-circle bg-label-' + $state + '">' + $initials + '</span>';


            var $detail_address = "/Admin/Agency/DetailOverview?id="+ full["id"];

            var $address = full["address"];
            // Creates full output for row
            var $row_output =
              '<div class="d-flex justify-content-start align-items-center customer-name">' +
              '<div class="avatar-wrapper">' +
              '<div class="avatar me-2">' +
              $output +
              '</div>' +
              '</div>' +
              '<div class="d-flex flex-column ms-1">' +
              '<a href="' +
              $detail_address +
              '" ><span class="fw-medium">' +
              $name +
              '</span></a>' +
              '<small class="text-muted">' +
              $address +
              '</small>' +
              '</div>' +
              '</div>';
            return $row_output;
          }
        },
        {
          // customer Role
          targets: 3,

          render: function (data, type, full, meta) {

            var $phone= full['admin_phone'];

            return "<span class='h6 mb-0'>" + $phone + '</span>';
          }
        },
        {
          // Plans
          targets: 4,
          render: function (data, type, full, meta) {
            var $allsoled= full['allsoled'];


            var $output_code = $allsoled;
           
            var $row_output =
              '<div class="d-flex justify-content-start align-items-center customer-country">' +
             
              '<div>' +
              '<span>' +
              $output_code +
              '</span>' +
              '</div>' +
              '</div>';
            return $row_output;
          }
        }
        
      ],
      order: [[4, 'desc']],
      dom:
        '<"card-header d-flex flex-wrap pb-md-2"' +
        '<"d-flex align-items-center me-5"f>' +
        '<"dt-action-buttons text-xl-end text-lg-start text-md-end text-start d-flex align-items-center justify-content-md-end gap-3 gap-sm-0 flex-wrap flex-sm-nowrap"lB>' +
        '>t' +
        '<"row mx-2"' +
        '<"col-sm-12 col-md-6"i>' +
        '<"col-sm-12 col-md-6"p>' +
        '>',
      language: {
        sLengthMenu: '_MENU_',
        search: '',
        searchPlaceholder: 'جستجو فروشنده'
      },
      // Buttons with Dropdown
      buttons: [
        {
          extend: 'collection',
          className: 'btn btn-label-secondary dropdown-toggle me-3 waves-effect waves-light',
          text: '<i class="ti ti-download me-1"></i>گرفتن خروجی',
          buttons: [
            {
              extend: 'print',
              text: '<i class="ti ti-printer me-2" ></i>چاپ',
              className: 'dropdown-item',
              exportOptions: {
                columns: [1, 2, 3, 4, 5, 6],
                // prevent avatar to be print
                format: {
                  body: function (inner, coldex, rowdex) {
                    if (inner.length <= 0) return inner;
                    var el = $.parseHTML(inner);
                    var result = '';
                    $.each(el, function (index, item) {
                      if (item.classList !== undefined && item.classList.contains('customer-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                      } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                      } else result = result + item.innerText;
                    });
                    return result;
                  }
                }
              },
              customize: function (win) {
                //customize print view for dark
                $(win.document.body)
                  .css('color', headingColor)
                  .css('border-color', borderColor)
                  .css('background-color', bodyBg);
                $(win.document.body)
                  .find('table')
                  .addClass('compact')
                  .css('color', 'inherit')
                  .css('border-color', 'inherit')
                  .css('background-color', 'inherit');
              }
            },
            {
              extend: 'csv',
              text: '<i class="ti ti-file me-2" ></i>Csv',
              className: 'dropdown-item',
              exportOptions: {
                columns: [1, 2, 3, 4, 5, 6],
                // prevent avatar to be display
                format: {
                  body: function (inner, coldex, rowdex) {
                    if (inner.length <= 0) return inner;
                    var el = $.parseHTML(inner);
                    var result = '';
                    $.each(el, function (index, item) {
                      if (item.classList !== undefined && item.classList.contains('customer-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                      } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                      } else result = result + item.innerText;
                    });
                    return result;
                  }
                }
              }
            },
            {
              extend: 'excel',
              text: '<i class="ti ti-file-export me-2"></i>Excel',
              className: 'dropdown-item',
              exportOptions: {
                columns: [1, 2, 3, 4, 5, 6],
                // prevent avatar to be display
                format: {
                  body: function (inner, coldex, rowdex) {
                    if (inner.length <= 0) return inner;
                    var el = $.parseHTML(inner);
                    var result = '';
                    $.each(el, function (index, item) {
                      if (item.classList !== undefined && item.classList.contains('customer-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                      } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                      } else result = result + item.innerText;
                    });
                    return result;
                  }
                }
              }
            },
            {
              extend: 'pdf',
              text: '<i class="ti ti-file-text me-2"></i>Pdf',
              className: 'dropdown-item',
              exportOptions: {
                columns: [1, 2, 3, 4, 5, 6],
                // prevent avatar to be display
                format: {
                  body: function (inner, coldex, rowdex) {
                    if (inner.length <= 0) return inner;
                    var el = $.parseHTML(inner);
                    var result = '';
                    $.each(el, function (index, item) {
                      if (item.classList !== undefined && item.classList.contains('customer-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                      } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                      } else result = result + item.innerText;
                    });
                    return result;
                  }
                }
              }
            },
            {
              extend: 'copy',
              text: '<i class="ti ti-copy me-2" ></i>کپی',
              className: 'dropdown-item',
              exportOptions: {
                columns: [1, 2, 3, 4, 5, 6],
                // prevent avatar to be display
                format: {
                  body: function (inner, coldex, rowdex) {
                    if (inner.length <= 0) return inner;
                    var el = $.parseHTML(inner);
                    var result = '';
                    $.each(el, function (index, item) {
                      if (item.classList !== undefined && item.classList.contains('customer-name')) {
                        result = result + item.lastChild.firstChild.textContent;
                      } else if (item.innerText === undefined) {
                        result = result + item.textContent;
                      } else result = result + item.innerText;
                    });
                    return result;
                  }
                }
              }
            }
          ]
        },
        {
          text: '<i class="ti ti-plus me-0 me-sm-1 mb-1 ti-xs"></i><span class="d-none d-sm-inline-block">افزودن فروشنده</span>',
          className: 'add-new btn btn-primary py-2 waves-effect waves-light',
          attr: {
            'data-bs-toggle': 'offcanvas',
            'data-bs-target': '#offcanvasEcommerceCustomerAdd'
          }
        }
      ],
      // For responsive popup
      responsive: {
        //details: {
        //  display: $.fn.dataTable.Responsive.display.modal({
        //    header: function (row) {
        //      var data = row.data();
        //      return 'جزئیات ' + data['name'];
        //    }
        //  }),
        //  type: 'column',
        //  renderer: function (api, rowIdx, columns) {
        //    var data = $.map(columns, function (col, i) {
        //      return col.title !== '' // ? Do not show row in modal popup if title is blank (for check box)
        //        ? '<tr data-dt-row="' +
        //            col.rowIndex +
        //            '" data-dt-column="' +
        //            col.columnIndex +
        //            '">' +
        //            '<td>' +
        //            col.title +
        //            ':' +
        //            '</td> ' +
        //            '<td>' +
        //            col.data +
        //            '</td>' +
        //            '</tr>'
        //        : '';
        //    }).join('');

        //    return data ? $('<table class="table"/><tbody />').append(data) : false;
        //  }
        //}
      }
    });
    $('.dataTables_length').addClass('ms-n2 mt-0 mt-md-3 me-2');
    $('.dt-action-buttons').addClass('pt-0');
    $('.dataTables_filter').addClass('ms-n3');
    $('.dt-buttons').addClass('d-flex flex-wrap');
  }

  // Delete Record
  $('.datatables-customers tbody').on('click', '.delete-record', function () {
    dt_customer.row($(this).parents('tr')).remove().draw();
  });

  // Filter form control to default size
  // ? setTimeout used for multilingual table initialization
  setTimeout(() => {
    $('.dataTables_filter .form-control').removeClass('form-control-sm');
    $('.dataTables_length .form-select').removeClass('form-select-sm');
  }, 300);
});

