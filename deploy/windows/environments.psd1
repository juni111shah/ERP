# Deploy targets on the Windows deploy PC. Dev is the developer workstation (VS on 5080 + ng serve on 4200, DB ERP_Dev)
# and is NOT managed by these scripts. No secrets in this file.
@{
    BaseRoot      = 'D:\ERP\envs'          # override with $env:ERP_ENVS_ROOT
    Cloudflared   = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'  # override with $env:ERP_CLOUDFLARED
    KeepReleases  = 5
    Environments  = @{
        staging = @{
            Port               = 5180
            Database           = 'ERP_Staging'
            AspNetEnvironment  = 'Staging'
            BackupBeforeDeploy = $false
            # PublicUrl is machine-specific: set it in <BaseRoot>\public-urls.psd1 (deploy\cloudflare\setup-named-tunnel.ps1), not here
        }
        production = @{
            Port               = 5280
            Database           = 'ERP_Prod'
            AspNetEnvironment  = 'Production'
            BackupBeforeDeploy = $true
            # PublicUrl is machine-specific: set it in <BaseRoot>\public-urls.psd1 (deploy\cloudflare\setup-named-tunnel.ps1), not here
        }
    }
}
