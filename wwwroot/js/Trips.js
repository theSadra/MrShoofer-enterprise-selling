function resolveTripImageUrl(Model) {
  const fallback = '/taxi.png';
  const imageValue = Model.Image || Model.image;
  if (!imageValue || typeof imageValue !== 'string' || imageValue.trim() === '') {
    return fallback;
  }

  const imagePath = imageValue.trim();

  // SearchJson already returns same-origin /media/ors/… — never re-host onto ORS.
  if (imagePath.startsWith('/media/ors') || imagePath === fallback || imagePath.startsWith('/taxi')) {
    return imagePath;
  }

  if (imagePath.startsWith('http://') || imagePath.startsWith('https://')) {
    try {
      const u = new URL(imagePath);
      if (u.pathname.startsWith('/images/')) {
        return '/media/ors' + u.pathname;
      }
    } catch { /* keep absolute URL */ }
    return imagePath;
  }

  if (imagePath.startsWith('/images/')) {
    return '/media/ors' + imagePath;
  }

  // Other same-origin absolute paths from the API
  if (imagePath.startsWith('/')) {
    return imagePath;
  }

  return imagePath;
}

function generateTripCard(Model) {

  // Check if this is a VIP car (taxiSupervisorID === 7 OR carModelName contains VIP/تشریفات OR specific VIP car models)
  const isVipCar = Model.taxiSupervisorID === 7 || 
                   (Model.carModelName && (Model.carModelName.includes('VIP') || 
                    Model.carModelName.includes('vip') || 
                    Model.carModelName.includes('تشریفات') ||
                    Model.carModelName.includes('آریو') ||
                    Model.carModelName.includes('اکسنت') ||
                    Model.carModelName.includes('جیلی') ||
                    Model.carModelName.includes('کمری') ||
                    Model.carModelName.includes('سفران') ||
                    Model.carModelName.includes('سوناتا')));

  const imageUrl = resolveTripImageUrl(Model);

  var html = `
        <div class="card trip-card px-2 px-md-4 py-3 mt-3" style="width: 100%;">

        <div class="mainbody">
            <div class="topinfo d-flex" style="justify-content: space-between; align-items:center; gap:0.5rem;">

                <div class="topinfo-car">
                    <span class="badge bg-label-secondary rounded-pill carmodel"> ${Model.carModelName} </span>
                    ${isVipCar ? '<img src="/vip_badge.png" style="height:29px;" class="ms-1" alt="" />' : ''}
                </div>

                <div class="capacity badge bg-label-secondary rounded-pill">
                    <i class="ti ti-armchair-2 d-inline" aria-hidden="true"></i>
                    <small class="text-danger ms-1 d-inline">دربستی</small>
                </div>
            </div>

            <div class="trip-info mt-3">

              <div class="trip-info-main d-flex justify-content-between">

                <div class="provider">
                    <img class="logo-image" src="${imageUrl}" width="56" height="56" alt="${Model.taxiSupervisorName || ''}" onerror="this.onerror=null;this.src='/taxi.png'">
                    <h5 class="mt-1 mb-0 trip-service-name">
                        ${Model.taxiSupervisorName}
                    </h5>
                </div>

                <div class="transport">
                    <div class="direction-container">

                        <div class="startendtime">
                            <span class="badge bg-label-secondary starttime-badge">
                              <span class="starttime">${Model.startingDateTime}</span>
                            </span>

                            <span class="trip-route-line" aria-hidden="true">
                               <span class="trip-route-dots">···</span>
                               <svg class="fa-taxiSide" viewBox="0 0 512 512" xmlns="http://www.w3.org/2000/svg"><path fill="currentColor" d="M480.6,179.2l-90.3-57.5c-7-4.5-14.8-7.5-23-8.8c-3.1-0.5-6.3-0.7-9.5-0.7h-68.5v-9.7c0-6.4-2.6-12.6-7.1-17.2  c-4.6-4.6-10.7-7.1-17.2-7.1c-6.4,0-12.6,2.6-17.2,7.1c-4.6,4.6-7.1,10.7-7.1,17.2v9.7h-6.7c-7.9,0-15.7,1.5-23,4.6  c-7.3,3-13.9,7.5-19.5,13.1l-62.3,61.6c-1.4,1.5-3.2,2.6-5.2,3.2c-0.2,0-0.3,0.1-0.5,0.1c-1.4,0.3-3.2,0.8-5.3,1.4l-32,10.6  c-3.7,1.2-7.7,2.4-12,3.5c-15.6,4.4-33.2,9.3-47.9,22.2C9.6,247.3,0,269.3,0,293.1V320c0,32.9,27,59.6,60.2,59.6h17.5  c3.2,15.3,11.7,29.1,23.9,39c12.2,9.9,27.4,15.3,43.1,15.3c15.7,0,30.9-5.4,43.1-15.3c12.2-9.9,20.6-23.6,23.9-39h88.9  c3.2,15.3,11.7,29.1,23.9,39c12.2,9.9,27.4,15.3,43.1,15.3s30.9-5.4,43.1-15.3c12.2-9.9,20.6-23.6,23.9-39h17.4  c33.2,0,60.2-26.7,60.2-59.6v-77.3C512,223.3,502.2,191.3,480.6,179.2z M456.2,221.3l0.2,0.1c0,0.1,0.1,0.2,0.1,0.2h-72.3v-46.1  L456.2,221.3z M225.7,164.3c1.1-1.2,2.4-2.1,3.8-2.7c1.4-0.6,3-0.9,4.6-0.9h101.6v60.9h-94.3c-20.6,0-40.9-4.9-59.3-14.2  L225.7,164.3z M144.6,385.3c-3.9,0-7.8-1.2-11-3.3c-3.3-2.2-5.8-5.3-7.3-8.9c-1.5-3.6-1.9-7.6-1.1-11.5  c0.8-3.9,2.7-7.4,5.4-10.2  c2.8-2.8,6.3-4.7,10.2-5.4c3.9-0.8,7.9-0.4,11.5,1.1c3.6,1.5,6.7,4.1,8.9,7.3c2.2,3.3,3.3,7.1,3.3,11  c0,5.3-2.1,10.3-5.8,14.1  C154.9,383.2,149.9,385.3,144.6,385.3z M367.4,385.3c-3.9,0-7.8-1.2-11-3.3c-3.3-2.2-5.8-5.3-7.3-8.9c-1.5-3.6-1.9-7.6-1.1-11.5  c0.8-3.9,2.7-7.4,5.4-10.2c2.8-2.8,6.3-4.7,10.2-5.4c3.9-0.8,7.9-0.4,11.5,1.1c3.6,1.5,6.7,4.1,8.9,7.3c2.2,3.3,3.3,7.1,3.3,11  c0,5.3-2.1,10.3-5.8,14.1C377.7,383.2,372.7,385.3,367.4,385.3L367.4,385.3z M451.8,331h-25.2c-6-10.4-14.6-19-25-24.9  c-10.4-6-22.1-9.1-34.1-9.1c-12,0-23.7,3.1-34.1,9.1c-10.4,6-19,14.6-25,24.9H203.7c-6-10.4-14.6-18.9-25-24.9  c-10.4-6-22.1-9.1-34.1-9.1c-12,0-23.7,3.1-34.1,9.1c-10.4,6-19,14.6-25,24.9H60.2c-6.4,0-11.7-5-11.7-11.1v-26.9  c0-9.6,3.7-18.6,9.9-24.1c6.4-5.6,17.4-8.7,29-11.9c4.6-1.3,9.4-2.6,14.2-4.2l31.9-10.6c0.2-0.1,0.5-0.1,0.7-0.2  c0.8-0.2,1.6-0.4,2.5-0.6c1.7-0.5,3.4-1,5.1-1.6c29.5,19.7,64.2,30.2,99.7,30.3h222V320C463.4,326.1,458.2,331,451.8,331L451.8,331z"></path></svg>
                               <span class="trip-route-dots">···</span>
                            </span>

                            <span class="endtime-wrap">
                              <span class="endtime">${Model.arrivalDateTime}</span>
                              <span class="text-muted endtime-approx">(تقریبی)</span>
                            </span>
                        </div>

                        <div class="direction">
                            <span class="directionval directionval--origin">${Model.origin}</span>
                            <span class="directionval directionval--dest">${Model.destination}</span>
                        </div>
                    </div>

                </div>
              </div>

                <div class="submitation d-flex">

                    <div class="prices mb-0 trip-price-stack">
                        ${Model.hasCommission ? `
                        <div class="trip-price-row trip-price-row--sale">
                            <span class="trip-price-label">قیمت پیشنهادی فروش</span>
                            <span class="trip-price-value orgprice">
                                <bdi class="trip-price-num">${Model.afterdiscount || Model.originalPrice || ''}</bdi>
                                <span class="trip-price-currency">تومان</span>
                            </span>
                        </div>
                        <div class="trip-price-row trip-price-row--pay">
                            <span class="trip-price-label">قیمت تمام شده</span>
                            <span class="trip-price-value price">
                                <bdi class="trip-price-num">${Model.payablePrice || Model.afterdiscount || ''}</bdi>
                                <span class="trip-price-currency">تومان</span>
                            </span>
                        </div>
                        <span class="trip-price-hint">کمیسیون ${Model.commissionPercent || ''}٪ کسر شده</span>
                        ` : `
                        <div class="trip-price-row trip-price-row--before">
                            <span class="trip-price-label">قیمت قبل از تخفیف</span>
                            <span class="trip-price-value orgprice orgprice--struck">
                                <bdi class="trip-price-num">${Model.originalPrice || Model.afterdiscount || ''}</bdi>
                                <span class="trip-price-currency">تومان</span>
                            </span>
                        </div>
                        <div class="trip-price-row trip-price-row--pay">
                            <span class="trip-price-label">قیمت بعد از تخفیف</span>
                            <span class="trip-price-value price">
                                <bdi class="trip-price-num">${Model.afterdiscount || Model.originalPrice || ''}</bdi>
                                <span class="trip-price-currency">تومان</span>
                            </span>
                        </div>
                        `}
                    </div>

                    <a href="/Reserve/Reservetrip?tripcode=${Model.tripcode}" class="btn btn-primary waves-effect waves-light trip-reserve-btn">رزرو سفر
                        <i class="ti ti-arrow-left ms-2" aria-hidden="true"></i>
                    </a>

                </div>
            </div>

        </div>
    </div>`;

  // Return the generated HTML
  return html;
}


function fetchTripsData() {
  let origin_city = $("#origin_input").val();
  let destination_city = $("#destination_input").val();
  let searchdate = $("#starttime").val();




  return new Promise((resolve, reject) => {
    $.ajax({
      url: `/TaxiTrips/SearchJson?originstring=${origin_city}&destinationstring=${destination_city}&searchdate=${searchdate}`, // Replace with your API endpoint
      method: 'GET',
      dataType: 'json',
      success: function (response) {
        // Debug: Log the first trip to see if Image property exists
        if (Array.isArray(response) && response.length > 0) {
          console.log('First trip from API:', response[0]);
          console.log('Image property:', response[0].Image);
          console.log('image property (lowercase):', response[0].image);
          resolve(response); // Resolve with the result
        } else {
          resolve([]); // Resolve with an empty array if no data
        }
      },
      error: function (xhr, status, error) {
        reject(error); // Reject the promise with an error
      }
    });
  });




}


let nottripfoundHtml = `<div class="d-flex col-12 mt-3" style="flex-direction: column; align-items: center; justify-content: start;">

							<label class="fs-4 fw-bold mt-4 pt-3">
							  ســفری یافت نشـد
							</label>
							<small>
							  در بازه ای که شما جست و جو کردید، سفری یافت نشد
							</small>
							</div>

           <div class="trips-container">
        </div>`;



// Avoid redeclaration error: only create if undefined
if (typeof window.trips === 'undefined') {
  window.trips = [];
}

function GetCarModels(tripsarr) {
  const carModelNames = tripsarr.map(trip => trip.carModelName);

  // Step 2: Get unique car models using a Set
  const uniqueCarModels = [...new Set(carModelNames)];


  return uniqueCarModels;
}



function carFilterSelectes(carmodel) {

  if (carmodel == 'default') {
    renderTrips(window.trips);

  }
  else {

    const filteredTrips = window.trips.filter(t => t.carModelName === carmodel)

  renderTrips(filteredTrips);
  }
  

}


function GenerateCarModelsFilter(carmodels) {
  // Render Bootstrap-like chips that stay in place
  const $container = $("#carmodelsfilter");
  if ($container.length === 0) return;

  // Clear existing
  $container.empty();

  // Keep chips in a row with modest spacing, no separators
  $container.addClass("d-flex flex-wrap gap-2");

  // Add 'All' chip — active = black fill + white text (readable)
  $container.append(
    '<button type="button" class="btn btn-sm rounded-pill car-chip car-chip--active" data-carmodel="default">همه</button>'
  );

  // Add a chip per unique car model
  carmodels.forEach(c => {
    const safeText = String(c).replace(/</g, "&lt;").replace(/>/g, "&gt;");
    // Check if this car model is VIP (includes VIP/تشریفات or specific VIP car models)
    const isVipCar = c && (c.includes('VIP') || c.includes('vip') || c.includes('تشریفات') ||
                          c.includes('آریو') || c.includes('اکسنت') || c.includes('جیلی') ||
                          c.includes('کمری') || c.includes('سفران') || c.includes('سوناتا'));
    const vipBadge = isVipCar ? '<img src="/vip_badge.png" style="height:16px;" class="ms-1" />' : '';
    $container.append(
      `<button type="button" class="btn btn-sm rounded-pill car-chip" data-carmodel="${safeText}">${safeText}${vipBadge}</button>`
    );
  });
}

$(function () {

  // Delegate click handling for chips without moving them or re-rendering the container
  $(document).on('click', '#carmodelsfilter .car-chip', function () {
    const model = $(this).data('carmodel');

    // Visual active state without layout shift
    $('#carmodelsfilter .car-chip').removeClass('car-chip--active');
    $(this).addClass('car-chip--active');

    carFilterSelectes(model);
  });

  fetchTripsData()
    .then(function (result) {
      window.trips = result;

      // no trips
      if (window.trips.length == 0) {
        $('.trips-container').empty();
        $('.trips-container').append(nottripfoundHtml);
      }
      else {

        renderTrips(window.trips);
        let carModels = GetCarModels(window.trips);
        GenerateCarModelsFilter(carModels);

      }
    })
    .catch(function (error) {
      console.error('Error fetching data:', error); // Handle the error here
    });
});

function renderTrips(input_trips) {
  $('.trips-container').empty();
  input_trips.forEach(t => { $('.trips-container').append(generateTripCard(t)) });
}


function tripSortPrice(trip) {
  const raw = (trip && (trip.payablePrice || trip.afterdiscount || '0')).toString().replace(/,/g, '');
  const n = parseInt(raw, 10);
  return Number.isFinite(n) ? n : 0;
}

function orderFilterSelected(number) {

  if (number == 0) {
    renderTrips(window.trips);
      
  }

  // Filter by price high to low
  if (number == 1) {
    const deccendingPrice_trips = [...window.trips]
      .sort((a, b) => tripSortPrice(b) - tripSortPrice(a));
    renderTrips(deccendingPrice_trips);

  }
  else if (number == 2) {
    const accendingPrice_trips = [...window.trips]
      .sort((a, b) => tripSortPrice(a) - tripSortPrice(b));
    renderTrips(accendingPrice_trips);
  }
}
