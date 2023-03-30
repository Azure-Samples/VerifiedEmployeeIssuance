var getVerifiedId = document.getElementById('get-VerifiedId');

function CreateIssuanceRequest() {
    fetch('/api/issuer/issuance-request')
        .then(function (response) {
            response.text()
                .catch(error => {
                    console.log(error.message);
                    showError(error.message);
                })
                .then(function (message) {
                    respIssuanceReq = JSON.parse(message);

                    if (Object.hasOwn(respIssuanceReq, 'error')) {
                        console.log(respIssuanceReq.error_description);
                        showError(respIssuanceReq.error_description);
                    }
                    else {
                        //window.location.href = respIssuanceReq.url;
                        window.location.href = "linkedin://openid-vc?url=" + encodeURIComponent(respIssuanceReq.url);
                        //if no redirect is happening you are not launched back to the native mobile app!
                        setTimeout(function () {
                            showError("You can't retrieve your Verified ID here. Try again on your mobile device.");
                        }, 1000);
                    }
                }).catch(error => {
                    console.log(error.message);
                    error => showError(error.message);
                })
        }).catch(error => {
            console.log("generic error:" + error.message);
            error => showError(error.message);
        })
}

function showError(message) {
    document.getElementById("progress-ring").style.display = "none";

    document.getElementById("error").style.display = "visible";
    //document.getElementById("errorMessage").style.display = "visible";
    //document.getElementById("errorMessage").classList.add("alert");
    //document.getElementById("errorMessage").classList.add("alert-primary");
    document.getElementById("errormessage").innerHTML = message;
}

CreateIssuanceRequest();
