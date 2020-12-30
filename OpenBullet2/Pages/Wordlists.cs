﻿using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenBullet2.Auth;
using OpenBullet2.Entities;
using OpenBullet2.Helpers;
using OpenBullet2.Repositories;
using OpenBullet2.Shared.Forms;
using Radzen.Blazor;
using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using System.Linq;
using GridBlazor.Pages;
using GridBlazor;
using GridShared;
using System;
using GridShared.Utility;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using GridMvc.Server;
using Microsoft.AspNetCore.Http;

namespace OpenBullet2.Pages
{
    public partial class Wordlists
    {
        [Inject] IModalService Modal { get; set; }
        [Inject] IWordlistRepository WordlistRepo { get; set; }
        [Inject] public IGuestRepository GuestRepo { get; set; }
        [Inject] AuthenticationStateProvider Auth { get; set; }

        private List<WordlistEntity> wordlists = new();
        private WordlistEntity selectedWordlist;
        private int uid = -1;

        private GridComponent<WordlistEntity> gridComponent;
        private CGrid<WordlistEntity> grid;
        private Task gridLoad;

        protected override async Task OnParametersSetAsync()
        {
            uid = await ((OBAuthenticationStateProvider)Auth).GetCurrentUserId();

            wordlists = uid == 0
                ? await WordlistRepo.GetAll().ToListAsync()
                : await WordlistRepo.GetAll().Include(w => w.Owner).Where(w => w.Owner.Id == uid).ToListAsync();

            Action<IGridColumnCollection<WordlistEntity>> columns = c =>
            {
                c.Add(w => w.Name).Titled(Loc["Name"]);
                c.Add(w => w.Type).Titled(Loc["Type"]);
                c.Add(w => w.Purpose).Titled(Loc["Purpose"]);
                c.Add(w => w.Total).Titled(Loc["Lines"]);
                c.Add(w => w.FileName).Titled(Loc["FileName"]);
            };

            var query = new QueryDictionary<StringValues>();
            query.Add("grid-page", "2");

            var client = new GridClient<WordlistEntity>(q => GetGridRows(columns, q), query, false, "wordlistsGrid", columns, CultureInfo.CurrentCulture)
                .Sortable()
                .Filterable()
                .SetKeyboard(true)
                .ChangePageSize(true)
                .WithGridItemsCount()
                .ExtSortable()
                .Selectable(true, false, false);
            grid = client.Grid;

            // Set new items to grid
            gridLoad = client.UpdateGrid();
            await gridLoad;
        }

        private ItemsDTO<WordlistEntity> GetGridRows(Action<IGridColumnCollection<WordlistEntity>> columns,
                QueryDictionary<StringValues> query)
        {
            var server = new GridServer<WordlistEntity>(wordlists, new QueryCollection(query),
                true, "wordlistsGrid", columns, 30).Sortable().Filterable().WithMultipleFilters();

            // Return items to displays
            return server.ItemsToDisplay;
        }

        protected void OnWordlistSelected(object item)
        {
            if (item.GetType() == typeof(WordlistEntity))
            {
                selectedWordlist = (WordlistEntity)item;
            }
        }

        private async Task RefreshList()
        {
            wordlists = uid == 0
                ? await WordlistRepo.GetAll().ToListAsync()
                : await WordlistRepo.GetAll().Include(w => w.Owner).Where(w => w.Owner.Id == uid).ToListAsync();

            await gridComponent.UpdateGrid();
            StateHasChanged();
        }

        private async Task AddWordlist()
        {
            var modal = Modal.Show<WordlistAdd>(Loc["AddWordlist"]);
            var result = await modal.Result;

            if (!result.Cancelled)
            {
                var entity = result.Data as WordlistEntity;
                entity.Owner = await GuestRepo.Get(uid);
                await WordlistRepo.Add(entity);
                wordlists.Add(entity);
                await js.AlertSuccess(Loc["Added"], Loc["AddedWordlist"]);
            }

            await RefreshList();
        }

        private async Task EditWordlist()
        {
            if (selectedWordlist == null)
            {
                await ShowNoWordlistSelectedWarning();
                return;
            }

            var parameters = new ModalParameters();
            parameters.Add(nameof(WordlistEdit.Wordlist), selectedWordlist);

            var modal = Modal.Show<WordlistEdit>(Loc["EditWordlist"], parameters);
            await modal.Result;

            await RefreshList();
        }

        private async Task DeleteWordlist()
        {
            if (selectedWordlist == null)
            {
                await ShowNoWordlistSelectedWarning();
                return;
            }

            if (await js.Confirm(Loc["AreYouSure"], $"{Loc["ReallyDelete"]} {selectedWordlist.Name}?", Loc["Cancel"]))
            {
                var deleteFile = await js.Confirm(Loc["AlsoDeleteFile"], 
                    $"{Loc["DeleteFileText1"]} {selectedWordlist.FileName} {Loc["DeleteFileText2"]}", Loc["KeepFile"]);

                // Delete the wordlist from the DB and disk
                await WordlistRepo.Delete(selectedWordlist, deleteFile);
            }

            await RefreshList();
        }

        private async Task ShowNoWordlistSelectedWarning()
            => await js.AlertError(Loc["Uh-Oh"], Loc["NoWordlistSelected"]);
    }
}
