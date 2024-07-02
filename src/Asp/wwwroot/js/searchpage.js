
var taxilocations = [
  { name: 'بیهقی', id: 'John' },
  { name: 'age', id: 30 },
  { name: 'city', id: 'New York' }
];

var staredlocations = [
  { name: 'تهران', cityid: 36 },
  { name: 'اصفهان', cityid: 36 },
  { name: 'رشت', cityid: 36 },
  { name: 'شیراز', cityid: 36 },
  { name: 'چالوس', cityid: 36 }
];

$(document).ready(function () {
  AddStaredLocationDiv_origin();
  AddStaredLocations_origin();
  AddStaredLocationDiv_desti();
  AddStaredLocations_dest();
});


function JustPrivateSelected() {

  var isprivateckb = $(".isprivate");

  var isprivate_title = $(".justprivate_title");

  if (isprivateckb.prop("checked")) {
    isprivate_title.addClass("section-title");
  }
  else {
    isprivate_title.removeClass("section-title");
  }
}

function AddStaredLocations_origin() {
  var terminals_container = $('#origincontainer');
  staredlocations.forEach(location => {
    var $aTag = $('<a>', {
      class: 'dropdown-item',
      text: location.name
    });
    $aTag.on('click', function () {
      OriginSelected(location.cityid, location.name);
    });
    terminals_container.append($aTag);
  });
}

function AddStaredLocations_dest() {
  var terminals_container = $('#desticontainer');
  staredlocations.forEach(location => {
    var $aTag = $('<a>', {
      class: 'dropdown-item',
      text: location.name
    });
    $aTag.on('click', function () {
      DestSelected(location.cityid, location.name);
    });
    terminals_container.append($aTag);
  });
}

function AddStaredLocationDiv_origin() {
  var spanElement = $('.origin_location');
  var starredLocationsHTML = `
            <div class="staredlocations">
            <label class="staredlocation_title ms-2 mt-2 text-muted pb-1">
                <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
                شهرهای پرتردد
            </label>
            <div class="px-1 .terminals_container_orig" id="origincontainer">
                
            </div>
            </div>

        `;
    spanElement.html(starredLocationsHTML);
}

function AddStaredLocationDiv_desti() {
  var spanElement = $('.dropdown-menu.destination_location');
  var starredLocationsHTML = `
            <div class="staredlocations">
            <label class="staredlocation_title ms-2 mt-2 text-muted pb-1">
                <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
                شهرهای پرتردد
            </label>
            <div class="px-1 .terminals_container_desti" id="desticontainer">
                
            </div>
            </div>
              
        `;
  spanElement.html(starredLocationsHTML);
}


function OriginSelected(id, name) {
  $('#origin_input').val(name).attr("value", id);
}

function DestSelected(id, name) {
  $('#destination_input').val(name).attr("value", id);
}



//document.getElementById('tripForm').addEventListener('submit', function (event) {
//  event.preventDefault(); // Prevent the default form submission

//  // Get the values from the input fields
//  const originValue = document.getElementById('origin_input').value;
//  const destinationValue = document.getElementById('destination_input').value;
//  const searchDateValue = document.getElementsByName('searchdate')[0].value;

//  // Construct the URL with query parameters
//  const url = `/TaxiTrips/Index?originstring=${encodeURIComponent(originValue)}&destinationstring=${encodeURIComponent(destinationValue)}&searchdate=${encodeURIComponent(searchDateValue)}`;

//  // Redirect the browser to the constructed URL
//  window.location.href = url;
//});
