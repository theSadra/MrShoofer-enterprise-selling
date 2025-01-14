let directions = [];
let originCities = [];
let _destinations = [];


function FetchDirections() {
  // Return a Promise
  return new Promise((resolve, reject) => {
    $.getJSON('/json/Directions/Directions.json', function (data) {
      directions = data;

      const cities = new Set();
      data.forEach(item => {
        cities.add(item.Cityone);
        cities.add(item.Citytwo);
      });

      originCities = Array.from(cities); // Update the global variable

      resolve(); // Resolve the promise when done
    }).fail(function () {
      console.error('Failed to fetch the JSON file.');
      reject('Error fetching JSON');
    });
  });
}


var most_used_origins = ["تهران", "اصفهان", "رشت", "چالوس", "کرمانشاه", "نوشهر"];

  
function SetDestinations(origin) {

  var destinations = [];
  directions.forEach(item => {
    if (item.Cityone == origin) {
      destinations.push(item.Citytwo);
    }
    else if (item.Citytwo == origin) {
      destinations.push(item.Cityone);
    }
  });

  _destinations = destinations;

  AddStaredLocationDiv_desti();
  AddResultLocations_destination(destinations);

}

function LoadMostUsedOrigins() {
  AddStaredLocationDiv_origin();
  $('#origin_most_lable').css('visibility', 'visible');
  AddResultLocations_origin(most_used_origins);
}

function AddStaredLocationDiv_origin() {
  var spanElement = $('.origin_location');
  var starredLocationsHTML = `
            <div class="staredlocations">
            <label class="staredlocation_title ms-2 mt-2 text-muted pb-1" id="origin_most_lable">
                <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
                شهرهای پرتردد
            </label>
            <div class="px-1 .terminals_container_orig" id="origincontainer">
                
            </div>
            </div>

        `;
  spanElement.html(starredLocationsHTML);
}



function FillTheDestinationWithPreSelectedOrigin(origin_location) {
  SetDestinations(origin_location);

}




function AddResultLocations_origin(result_locations) {

  var terminals_container = $('#origincontainer');
    terminals_container.empty();

  if (result_locations.length == 0) {

    var $aTag = $('<a>', {
      class: 'dropdown-item text-center mt-2 text-muted',
      text: "نتیجه‌ای پیدا نشد"
    });

    terminals_container.append($aTag);
  }
  else {

    result_locations.forEach(location => {
      var $aTag = $('<a>', {
        class: 'dropdown-item',
        text: location
      });
      $aTag.on('click', function () {
        OriginSelected(0, location);
      });
      terminals_container.append($aTag);
    });
  }
}


function AddResultLocations_destination(result_locations) {



  var terminals_container = $('#desticontainer');
  terminals_container.empty();
  result_locations.forEach(location => {
    var $aTag = $('<a>', {
      class: 'dropdown-item',
      text: location
    });
    $aTag.on('click', function () {
      DestSelected(0, location);
    });
    terminals_container.append($aTag);
  });
}


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
  originCities.forEach(location => {
    var $aTag = $('<a>', {
      class: 'dropdown-item',
      text: location
    });
    $aTag.on('click', function () {
      OriginSelected(0, location);
    });
    terminals_container.append($aTag);
  });
}




function AddStaredLocations_dest() {
  var terminals_container = $('#desticontainer');
  originCities.forEach(location => {
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


function AddStaredLocationDiv_desti() {
  var spanElement = $('.dropdown-menu.destination_location');
  var starredLocationsHTML = `
            <div class="staredlocations">
            <label class="staredlocation_title ms-2 mt-2 text-muted pb-1">
                <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
                مقصد ها
            </label>
            <div class="px-1 .terminals_container_desti" id="desticontainer">
            </div>
            </div>
              
        `;
  spanElement.html(starredLocationsHTML);
}


function OriginSelected(id, name) {
  $('#origin_input').val(name).attr("value", id);

  EnableDestination();

  SetDestinations(name);
}

function DestSelected(id, name) {
  $('#destination_input').val(name).attr("value", id);
}







function DisableDestination() {
  $("#destination_input").removeAttr("data-bs-toggle")
    .prop("disabled", true);

}

function EnableDestination() {
  $("#destination_input").attr("data-bs-toggle", "dropdown")
    .prop("disabled", false);
}












$(document).ready(async function () {

  try {
    // Await the FetchDirections call
    await FetchDirections();



    var origin_value = $('#origin_input').val();

  
    // if the value of origin is pre filled by the server 
    if (origin_value!= "") {
      LoadMostUsedOrigins();
      FillTheDestinationWithPreSelectedOrigin(origin_value);

      EnableDestination();   
    }
    else {

      // Call dependent functions
      LoadMostUsedOrigins()
      AddStaredLocationDiv_desti();
      AddStaredLocations_dest();


      DisableDestination();
    }

  } catch (error) {
    console.error('An error occurred:', error);
  }

  $('#origin_input').on('input', function () {
    const inputText = $(this).val(); // Get the current input valu
    // Filter cities that include the input texte

    if (inputText === "") {
      LoadMostUsedOrigins();
    }
    else {

      $('#origin_most_lable').css('display', 'none');
      const filteredCities = originCities.filter(city =>
        city.includes(inputText) // Check if city contains the input text
      );

      AddResultLocations_origin(filteredCities);
    }
  });



  $('#destination_input').on('input', function () {
    const inputText = $(this).val(); // Get the current input valu
    // Filter cities that include the input texte

    if (inputText === "") {

    }
    else {

      const filteredCities = _destinations.filter(city =>
        city.includes(inputText) // Check if city contains the input text
      );

      AddResultLocations_destination(filteredCities);
    }
  });


});



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
