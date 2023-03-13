$TenantID="<YOUR TENANTID>"
$DisplayNameOfMSI="<NAME OF THE MANAGED IDENTITY FROM YOUR AZURE WEBAPP>"

$ApiAppId = "3db474b9-6a0c-4840-96ac-1fceb342124f"
$PermissionName = "VerifiableCredential.Create.All"
 

# Install the module
Install-Module AzureAD

Connect-AzureAD -TenantId $TenantID

$MSI = (Get-AzureADServicePrincipal -Filter "displayName eq '$DisplayNameOfMSI'")

Start-Sleep -Seconds 10

$ApiServicePrincipal = Get-AzureADServicePrincipal -Filter "appId eq '$ApiAppId'"
$AppRole = $ApiServicePrincipal.AppRoles | Where-Object {$_.Value -eq $PermissionName -and $_.AllowedMemberTypes -contains "Application"}
New-AzureAdServiceAppRoleAssignment -ObjectId $MSI.ObjectId -PrincipalId $MSI.ObjectId ` -ResourceId $ApiServicePrincipal.ObjectId -Id $AppRole.Id