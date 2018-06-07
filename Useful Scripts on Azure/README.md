<table>
<tr>
<td>Task</td>   <td>Script(s)</td>   <td> Documentation </td>
</tr>

<tr>
<td>Bulk Sandbox Deployment Automation</td>
<td>change-vm-state.sh</p>create-vm-image.sh</p>deploy-vm.sh</td>
<td>These scripts achieve the following user stories:</p>
Ability to deploy multiple DSVMs (Data Science Virtual Machine) in either Windows, Ubuntu, of Linux CentOS</p>
Ability to deploy VMs from marketplace image, custom image, or DSVM</p>
Ability to deploy VMs across one or multiple subscriptions (i.e. Each VM can be in its own subscription)</p>
Ability for each VM to have its own Resource Group</p>
Ability to create a clean VM image from a VM
</td>
</tr>

<tr>
<td> Create Azure Resource Groups and sign user access. Perfect solution for Group projects</td>
<td> EduPortalAzureBulkCreate.psm1</p> NewUsers.csv</p>NewResourceGroups.csv</td>
<td> This is a set of Powershell scripts which will help you perform tasks on your azure subscriptions. Simply run the command functions within Powershell.</p> 
Functions:
<ul> 
<li><b>New-Users</b> - Creates new users based on content of the CSV</li>  
<li><b>New-ResourceGroups</b> - Creates new resource group and add users based on CSV content</li>
<li><b>Set-RBACPermissions</b> - creates role based access control permission based on CSV</li>
</ul>
</td>
</tr>

<tr>
<td> Bulk Adding AAD Existing users via Powershell</td>
<td> BulkAddAAD.ps1</p> Data.txt</td>
<td> This Powershell script reads a list of accounts to a .txt file, then assigns an Azure subscription to each of the developers.
Full instructions can be found in the file BulkAddAADInstructions.md.</p>
<b>IMPORTANT NOTE:</b> Accounts created with this script will NOT be able to access the <a href="https://manage.windowsazure.com"> Azure classic portal</a>.</p>
This script creates accounts based on Role Based Access Control (RBAC) and puts limitations on the accounts created to a contributor. You can see a complete list of features available in 
the Azure portal vs the Azure classic portal in this <a href="https://azure.microsoft.com/en-us/features/azure-portal/availability/">Azure portal availability chart</a>.</td>
</tr>

<tr>
<td> Listing all your Azure Subscriptions via Powershell</td>
<td> ListAzureSubs.ps1 </td>
<td> This Powershell script lists the Azure Subscriptions and then lists a table format of the Azure SubscriptionName and Azure SubscriptionID. Simply run the commands.</p>
Navigate to the directory where the script lives.</p>
PS> cd C:\my_path\yada_yada\ (enter)</p>
Execute the script:</p>
PS> .\ListAzureSubs.ps1 (enter)</p>
Or you can run the PowerShell script from cmd.exe like this:</p>
powershell -noexit C:\my_path\yada_yada\ListAzureSubs.ps1 (enter)</p>
Then copy and paste your Subscription IDs into the Excel for assigning users.
</td>
</tr>

<tr>
<td> Useful Comamds Azure Subscription via Powershell</td>
<td> AzureSubsCommands.md </td>
<td> This is a set of Powershell scripts which will help you perform tasks on your Azure subscriptions. 
Simply run the commands within Powershell.</td>
</tr>

</table>
